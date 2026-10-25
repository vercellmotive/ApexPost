using ApexPost.Application.Interfaces;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize]
public class CatalogController : Controller
{
    private readonly ICatalogueService _catalogue;
    private readonly IRepository<BranchStock> _stocks;

    public CatalogController(ICatalogueService catalogue, IRepository<BranchStock> stocks)
    {
        _catalogue = catalogue;
        _stocks = stocks;
    }

    [HttpGet("/Catalog")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var products = await _catalogue.GetProductsAsync(ct);
        var branchCode = User.FindFirstValue("BranchCode");
        IReadOnlyList<BranchStock> stock = branchCode is null
            ? Array.Empty<BranchStock>()
            : await _stocks.GetAllAsync(ct);
        var model = products.Select(product => new ProductStockViewModel(
            product,
            stock.FirstOrDefault(item => item.BranchCode.Value == branchCode && item.ProductSku == product.Id)?.QuantityOnHand ?? 0))
            .ToList();
        return View(model);
    }
}
