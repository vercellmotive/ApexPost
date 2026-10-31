using ApexPost.Application.Queries;
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
    private const int RecentTransactionLimit = 200;
    private readonly TransactionService _transactionService;
    private readonly IScreenQueries _queries;
    private readonly IAuthorizationService _authorizationService;

    public CounterController(
        TransactionService transactionService,
        IScreenQueries queries,
        IAuthorizationService authorizationService)
    {
        _transactionService = transactionService;
        _queries = queries;
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

        var model = new TransactionFormViewModel();
        await PopulateCheckoutViewAsync(model, branchCode, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Transactions(CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        if (branchCode is null)
        {
            return Forbid();
        }

        return View(await _queries.GetBranchTransactionsAsync(branchCode, RecentTransactionLimit, ct));
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
            throw new DomainValidationException("Each retail item needs both a product and a quantity.");
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
                shipment.ServiceLevel,
                shipment.ToDestination()))
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
            await PopulateCheckoutViewAsync(model, branchCode, ct);
            return View("Index", model);
        }
        catch (UnauthorizedDomainActionException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("Void")]
    public async Task<IActionResult> Void(TransactionReversalRequest request, CancellationToken ct)
    {
        HttpContext.AddAuditDetail("transactionId", request.TransactionId!.Value.ToString("D"));
        if (!(await _authorizationService.AuthorizeAsync(User, "BranchManagerOnly")).Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var managerId = User.FindFirstValue("StaffId");
        if (managerId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        await _transactionService.VoidAsync(request.TransactionId.Value, managerId, request.Reason, ct);
        TempData["Success"] = "Transaction voided.";
        return RedirectToAction(nameof(Transactions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("Refund")]
    public async Task<IActionResult> Refund(TransactionReversalRequest request, CancellationToken ct)
    {
        HttpContext.AddAuditDetail("transactionId", request.TransactionId!.Value.ToString("D"));
        if (!(await _authorizationService.AuthorizeAsync(User, "BranchManagerOnly")).Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var managerId = User.FindFirstValue("StaffId");
        if (managerId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        await _transactionService.RefundAsync(request.TransactionId.Value, managerId, request.Reason, ct);
        TempData["Success"] = "Transaction refunded.";
        return RedirectToAction(nameof(Transactions));
    }

    public Task RepopulateFormAsync(object model, CancellationToken ct) =>
        model is TransactionFormViewModel form && User.FindFirstValue("BranchCode") is { } branchCode
            ? PopulateCheckoutViewAsync(form, branchCode, ct)
            : Task.CompletedTask;

    private async Task PopulateCheckoutViewAsync(TransactionFormViewModel model, string branchCode, CancellationToken ct)
    {
        model.Options = await _queries.GetCheckoutOptionsAsync(branchCode, ct);
    }
}
