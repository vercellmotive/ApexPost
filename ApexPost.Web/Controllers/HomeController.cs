using ApexPost.Application.Services;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace ApexPost.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly DashboardService _dashboardService;

        public HomeController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [Authorize(Policy = "SameBranchOnly")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var branchCode = User.IsInRole("Auditor") ? null : User.FindFirstValue("BranchCode");
            if (branchCode is null && !User.IsInRole("Auditor"))
            {
                return Forbid();
            }

            var snapshot = await _dashboardService.BuildAsync(branchCode, ct);
            return View(snapshot);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
