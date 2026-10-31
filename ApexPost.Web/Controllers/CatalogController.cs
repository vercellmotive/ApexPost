using ApexPost.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class CatalogController : Controller
{
    private readonly IScreenQueries _queries;

    public CatalogController(IScreenQueries queries)
    {
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        if (branchCode is null)
        {
            return Forbid();
        }

        return View(await _queries.GetBranchCatalogueAsync(branchCode, ct));
    }
}
