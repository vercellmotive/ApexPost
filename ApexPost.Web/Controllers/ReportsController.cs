using ApexPost.Application.Interfaces;
using ApexPost.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ReportingService _reportingService;
    private readonly IAuthorizationService _authorizationService;

    public ReportsController(
        ReportingService reportingService,
        IAuthorizationService authorizationService)
    {
        _reportingService = reportingService;
        _authorizationService = authorizationService;
    }

    [HttpGet("/Reports")]
    [Authorize(Policy = "SameBranchOnly")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var canViewAllBranches = User.IsInRole("Auditor");
        var branchCode = User.FindFirstValue("BranchCode");
        if (branchCode is null)
        {
            return Forbid();
        }

        var reportBranch = canViewAllBranches ? null : branchCode;
        var sales = await _reportingService.GetDailyBranchSalesAsync(reportBranch, ct);
        var stock = await _reportingService.GetLowStockReportAsync(5, reportBranch, ct);
        var shipping = await _reportingService.GetShippingVolumeByZoneAsync(reportBranch, ct);
        ViewBag.Sales = sales;
        ViewBag.Stock = stock;
        ViewBag.Shipping = shipping;
        return View();
    }

    [HttpGet("/Reports/Audit")]
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
