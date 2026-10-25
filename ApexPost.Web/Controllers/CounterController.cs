using ApexPost.Application.Interfaces;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class CounterController : Controller
{
    private readonly TransactionService _transactionService;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<BranchStock> _stocks;
    private readonly IRepository<Product> _products;
    private readonly IAuthorizationService _authorizationService;

    public CounterController(
        TransactionService transactionService,
        IRepository<Branch> branches,
        IRepository<BranchStock> stocks,
        IRepository<Product> products,
        IAuthorizationService authorizationService)
    {
        _transactionService = transactionService;
        _branches = branches;
        _stocks = stocks;
        _products = products;
        _authorizationService = authorizationService;
    }

    [HttpGet("/Counter")]
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

    [HttpPost("/Counter/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var requestedBranchCode = User.FindFirstValue("BranchCode");
            if (requestedBranchCode is null)
            {
                return Forbid();
            }

            await PopulateCheckoutViewAsync(requestedBranchCode, ct);
            return View("Index", model);
        }

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

        try
        {
            var transactionId = Guid.NewGuid();
            HttpContext.Items["Audit:TransactionId"] = transactionId;
            var shipping = model.IncludeShipping
                ? new ShippingDetails(
                    model.IsParcel,
                    Weight.FromKilograms(model.ShippingWeightKg),
                    new Dimensions(model.LengthCm, model.WidthCm, model.HeightCm),
                    model.DestinationZone,
                    model.ServiceLevel)
                : null;
            await _transactionService.CheckoutAsync(
                branchCode,
                clerkId,
                new CounterCheckoutRequest(
                    model.CounterId,
                    retailItems,
                    shipping,
                    model.MoneyOrderAmount,
                    model.Sender,
                    model.Recipient,
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

    [HttpPost("/Counter/Void")]
    [ValidateAntiForgeryToken]
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
            return RedirectToAction(nameof(Index));
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

    [HttpPost("/Counter/Refund")]
    [ValidateAntiForgeryToken]
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
            return RedirectToAction(nameof(Index));
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

    private async Task PopulateCheckoutViewAsync(string branchCode, CancellationToken ct)
    {
        ViewBag.Branches = (await _branches.GetAllAsync(ct))
            .Where(branch => branch.Id.Value == branchCode)
            .ToList();
        ViewBag.Stocks = await _stocks.GetAllAsync(ct);
        ViewBag.Products = await _products.GetAllAsync(ct);
    }
}
