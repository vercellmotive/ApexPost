using ApexPost.Application.Queries;
using ApexPost.Web.Models;
using ApexPost.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportQueries _reportingService;
    private readonly IAuthorizationService _authorizationService;
    private readonly LowStockRule _lowStockRule;

    public ReportsController(
        IReportQueries reportingService,
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
        return View(new ReportsViewModel(
            reportDate,
            _lowStockRule.Threshold,
            await _reportingService.GetDailyBranchSalesAsync(reportBranch, reportDate, ct),
            await _reportingService.GetLowStockAsync(reportBranch, _lowStockRule.Threshold, ct),
            await _reportingService.GetShippingVolumeAsync(reportBranch, ct)));
    }

    [HttpGet]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        if (!(await _authorizationService.AuthorizeAsync(User, "AuditorOnly")).Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var report = await _reportingService.GetMoneyOrderAuditAsync(null, ct);
        return View(report);
    }
}
