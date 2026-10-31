using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Web.Attributes;
using ApexPost.Web.Filters;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class CounterController : Controller, IRepopulatesForm
{
    private readonly TransactionService _transactionService;
    private readonly IAuthorizationService _authorizationService;

    public CounterController(
        TransactionService transactionService,
        IAuthorizationService authorizationService)
    {
        _transactionService = transactionService;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        if (branchCode is null)
        {
            return Forbid();
        }

        await PopulateCheckoutViewAsync(branchCode, ct);
        return View(new TransactionFormViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Transactions(CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        if (branchCode is null)
        {
            return Forbid();
        }

        return View(await _transactionService.GetBranchTransactionsAsync(branchCode, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Index")]
    [AuditedAction("Sale")]
    public async Task<IActionResult> Create(TransactionFormViewModel model, CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        var clerkId = User.FindFirstValue("StaffId");
        if (branchCode is null || clerkId is null)
        {
            return Forbid();
        }

        var hasIncompleteRetailLine = model.RetailItems.Any(item =>
            string.IsNullOrWhiteSpace(item.ProductSku) != (item.Quantity is null));
        if (hasIncompleteRetailLine)
        {
            return BadRequest("Each retail item needs both a product and a quantity.");
        }

        var retailItems = model.RetailItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductSku))
            .Select(item => new RetailItemRequest(new Sku(item.ProductSku!.Trim()), item.Quantity!.Value))
            .ToList();

        var shipments = model.Shipments
            .Where(shipment => shipment.Include)
            .Select(shipment => new ShippingDetails(
                shipment.IsParcel,
                Weight.FromKilograms(shipment.WeightKg),
                new Dimensions(shipment.LengthCm, shipment.WidthCm, shipment.HeightCm),
                shipment.DestinationZone,
                shipment.ServiceLevel))
            .ToList();
        var moneyOrders = model.MoneyOrders
            .Where(order => order.Amount is not null
                || !string.IsNullOrWhiteSpace(order.Sender)
                || !string.IsNullOrWhiteSpace(order.Recipient))
            .Select(order => new MoneyOrderRequest(order.Amount ?? 0m, order.Sender, order.Recipient))
            .ToList();

        try
        {
            var transactionId = Guid.NewGuid();
            HttpContext.AddAuditDetail("transactionId", transactionId.ToString("D"));
            HttpContext.AddAuditDetail("counterId", model.CounterId);
            HttpContext.AddAuditDetail("retailLines", retailItems.Count.ToString(CultureInfo.InvariantCulture));
            HttpContext.AddAuditDetail("shipments", shipments.Count.ToString(CultureInfo.InvariantCulture));
            HttpContext.AddAuditDetail("moneyOrders", moneyOrders.Count.ToString(CultureInfo.InvariantCulture));
            HttpContext.AddAuditDetail("moneyOrderAmount", moneyOrders.Sum(order => order.Amount).ToString("0.00", CultureInfo.InvariantCulture));
            HttpContext.AddAuditDetail("identityVerification", model.IncludeIdentityVerification.ToString());
            HttpContext.AddAuditDetail("paymentMethod", model.PaymentMethod);
            await _transactionService.CheckoutAsync(
                branchCode,
                clerkId,
                new CounterCheckoutRequest(
                    model.CounterId,
                    retailItems,
                    shipments,
                    moneyOrders,
                    model.IncludeIdentityVerification,
                    model.DocumentType,
                    model.DocumentNumber,
                    model.PaymentMethod),
                transactionId,
                ct);
            TempData["Success"] = "Transaction created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateCheckoutViewAsync(branchCode, ct);
            return View("Index", model);
        }
        catch (UnauthorizedDomainActionException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("Void")]
    public async Task<IActionResult> Void(Guid transactionId, string reason, CancellationToken ct)
    {
        if (!(await _authorizationService.AuthorizeAsync(User, "BranchManagerOnly")).Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var managerId = User.FindFirstValue("StaffId");
        if (managerId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        try
        {
            await _transactionService.VoidAsync(transactionId, managerId, reason, ct);
            TempData["Success"] = "Transaction voided.";
            return RedirectToAction(nameof(Transactions));
        }
        catch (UnauthorizedDomainActionException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("Refund")]
    public async Task<IActionResult> Refund(Guid transactionId, string reason, CancellationToken ct)
    {
        if (!(await _authorizationService.AuthorizeAsync(User, "BranchManagerOnly")).Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var managerId = User.FindFirstValue("StaffId");
        if (managerId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        try
        {
            await _transactionService.RefundAsync(transactionId, managerId, reason, ct);
            TempData["Success"] = "Transaction refunded.";
            return RedirectToAction(nameof(Transactions));
        }
        catch (UnauthorizedDomainActionException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public Task RepopulateFormAsync(object model, CancellationToken ct) =>
        User.FindFirstValue("BranchCode") is { } branchCode
            ? PopulateCheckoutViewAsync(branchCode, ct)
            : Task.CompletedTask;

    private async Task PopulateCheckoutViewAsync(string branchCode, CancellationToken ct)
    {
        var options = await _transactionService.GetCheckoutOptionsAsync(branchCode, ct);
        ViewBag.Branches = new[] { options.Branch };
        ViewBag.Stocks = options.Stock;
        ViewBag.Products = options.Products;
    }
}
