using ApexPost.Application.Interfaces;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ReportingService _reportingService;
    private readonly IAuthorizationService _authorizationService;
    private readonly LowStockRule _lowStockRule;

    public ReportsController(
        ReportingService reportingService,
        IAuthorizationService authorizationService,
        LowStockRule lowStockRule)
    {
        _reportingService = reportingService;
        _authorizationService = authorizationService;
        _lowStockRule = lowStockRule;
    }

    [HttpGet]
    [Authorize(Policy = "SameBranchOnly")]
    public async Task<IActionResult> Index(string? branchCode, DateOnly? date, CancellationToken ct)
    {
        var assignedBranch = User.FindFirstValue("BranchCode");
        if (assignedBranch is null)
        {
            return Forbid();
        }

        var reportBranch = !string.IsNullOrWhiteSpace(branchCode)
            ? branchCode
            : User.IsInRole("Auditor") ? null : assignedBranch;
        var reportDate = date
            ?? await _reportingService.GetLatestSalesDateAsync(reportBranch, ct)
            ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var salesTask = _reportingService.GetDailyBranchSalesAsync(reportBranch, ct, reportDate);
        var stockTask = _reportingService.GetLowStockReportAsync(_lowStockRule.Threshold, reportBranch, ct);
        var shippingTask = _reportingService.GetShippingVolumeByZoneAsync(reportBranch, ct);
        await Task.WhenAll(salesTask, stockTask, shippingTask);
        ViewBag.ReportDate = reportDate;
        ViewBag.Sales = await salesTask;
        ViewBag.Stock = await stockTask;
        ViewBag.LowStockThreshold = _lowStockRule.Threshold;
        ViewBag.Shipping = await shippingTask;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        if (!(await _authorizationService.AuthorizeAsync(User, "AuditorOnly")).Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var report = await _reportingService.GetMoneyOrderAuditReportAsync(null, ct);
        return View(report);
    }
}
