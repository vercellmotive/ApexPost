using System.Globalization;
using System.Security.Claims;
using ApexPost.Application.Queries;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Web.Attributes;
using ApexPost.Web.Filters;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ServicesController : Controller, IRepopulatesForm
{
    private readonly TransactionService _transactions;
    private readonly IScreenQueries _queries;

    public ServicesController(TransactionService transactions, IScreenQueries queries)
    {
        _transactions = transactions;
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> MoneyOrder(CancellationToken ct)
    {
        await PopulateCountersAsync(ct);
        return View(new MoneyOrderFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid]
    [AuditedAction("MoneyOrder")]
    public async Task<IActionResult> MoneyOrder(MoneyOrderFormViewModel model, CancellationToken ct)
    {
        HttpContext.AddAuditDetail("counterId", model.CounterId);
        HttpContext.AddAuditDetail("moneyOrderAmount", model.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        HttpContext.AddAuditDetail("identityVerification", model.IncludeIdentityVerification.ToString());
        HttpContext.AddAuditDetail("paymentMethod", model.PaymentMethod);
        var request = new CounterCheckoutRequest(
            model.CounterId,
            [],
            [],
            [new MoneyOrderRequest(model.Amount, model.Sender, model.Recipient)],
            model.IncludeIdentityVerification,
            model.DocumentType,
            model.DocumentNumber,
            model.PaymentMethod);
        var outcome = await CheckoutOrRedisplayAsync(request, model, ct);
        if (outcome.Result is not null)
        {
            return outcome.Result;
        }

        TempData["Success"] = $"Money order issued. Total paid: {outcome.Transaction!.GrandTotal.Amount:C}.";
        return RedirectToAction(nameof(MoneyOrder));
    }

    [HttpGet]
    public async Task<IActionResult> IdentityVerification(CancellationToken ct)
    {
        await PopulateCountersAsync(ct);
        return View(new IdentityVerificationFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid]
    [AuditedAction("IdentityVerification")]
    public async Task<IActionResult> IdentityVerification(IdentityVerificationFormViewModel model, CancellationToken ct)
    {
        HttpContext.AddAuditDetail("counterId", model.CounterId);
        HttpContext.AddAuditDetail("documentType", model.DocumentType.ToString());

        // A standalone check has no charge, so the basket is settled as a zero-value cash transaction.
        var request = new CounterCheckoutRequest(
            model.CounterId,
            [],
            [],
            [],
            IncludeIdentityVerification: true,
            model.DocumentType,
            model.DocumentNumber,
            "Cash");
        var outcome = await CheckoutOrRedisplayAsync(request, model, ct);
        if (outcome.Result is not null)
        {
            return outcome.Result;
        }

        var verification = outcome.Transaction!.LineItems
            .First(item => item.ItemType == LineItemType.IdentityVerification).IdVerification!;
        HttpContext.AddAuditDetail("verified", verification.IsVerified.ToString());
        TempData["Success"] = verification.IsVerified
            ? $"Identity check passed for {verification.DocType} {verification.MaskedDocNumber}."
            : $"Identity check failed for {verification.DocType} {verification.MaskedDocNumber}.";
        return RedirectToAction(nameof(IdentityVerification));
    }

    public Task RepopulateFormAsync(object model, CancellationToken ct) => PopulateCountersAsync(ct);

    private async Task<(IActionResult? Result, Transaction? Transaction)> CheckoutOrRedisplayAsync(
        CounterCheckoutRequest request,
        object model,
        CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        var staffId = User.FindFirstValue("StaffId");
        if (branchCode is null || staffId is null)
        {
            return (Forbid(), null);
        }

        var transactionId = Guid.NewGuid();
        HttpContext.AddAuditDetail("transactionId", transactionId.ToString("D"));
        try
        {
            return (null, await _transactions.CheckoutAsync(branchCode, staffId, request, transactionId, ct));
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateCountersAsync(ct);
            return (View(model), null);
        }
        catch (UnauthorizedDomainActionException)
        {
            return (Forbid(), null);
        }
        catch (KeyNotFoundException)
        {
            return (NotFound(), null);
        }
    }

    private async Task PopulateCountersAsync(CancellationToken ct)
    {
        ViewBag.Counters = User.FindFirstValue("BranchCode") is { } branchCode
            ? (await _queries.GetCheckoutOptionsAsync(branchCode, ct))?.CounterTerminals ?? []
            : [];
    }
}
