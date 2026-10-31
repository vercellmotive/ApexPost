using ApexPost.Application.Interfaces;
using ApexPost.Application.Services;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class CatalogController : Controller
{
    private readonly ICatalogueService _catalogue;
    private readonly InventoryService _inventory;

    public CatalogController(ICatalogueService catalogue, InventoryService inventory)
    {
        _catalogue = catalogue;
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var branchCode = User.FindFirstValue("BranchCode");
        if (branchCode is null)
        {
            return Forbid();
        }

        var catalogue = await _inventory.GetBranchCatalogueAsync(_catalogue, branchCode, ct);
        return View(catalogue.Select(item => new ProductStockViewModel(item.Product, item.QuantityOnHand)).ToList());
    }
}
