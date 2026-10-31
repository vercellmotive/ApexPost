using ApexPost.Application.Queries;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Web.Attributes;
using ApexPost.Web.Filters;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "AdministratorOnly")]
[IpRestricted]
public sealed class ProductsController : Controller, IRepopulatesForm
{
    private readonly ProductManagementService _products;
    private readonly IScreenQueries _queries;

    public ProductsController(ProductManagementService products, IScreenQueries queries)
    {
        _products = products;
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _queries.GetProductsAsync(ct));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Form", new ProductFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Form")]
    [AuditedAction("ProductCreate")]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        try
        {
            await _products.CreateAsync(ToInput(model), ct);
            TempData["Success"] = "Product added.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var product = await _queries.GetProductAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        return View("Form", new ProductFormViewModel
        {
            IsExisting = true,
            Sku = product.Sku,
            Name = product.Name,
            Category = product.Category,
            UnitPrice = product.UnitPrice,
            TaxCategory = product.TaxCategory
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Form")]
    [AuditedAction("ProductUpdate")]
    public async Task<IActionResult> Edit([FromRoute] string id, ProductFormViewModel model, CancellationToken ct)
    {
        model.IsExisting = true;
        model.Sku = id;
        try
        {
            await _products.UpdateAsync(id, ToInput(model), ct);
            TempData["Success"] = "Product updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("PriceRevision")]
    public async Task<IActionResult> RevisePrices(PriceRevisionViewModel model, CancellationToken ct)
    {
        try
        {
            var revised = await _products.RevisePricesAsync(model.Category, model.PercentChange, ct);
            HttpContext.AddAuditDetail("category", model.Category.Replace(' ', '-'));
            HttpContext.AddAuditDetail("percentChange", model.PercentChange.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            HttpContext.AddAuditDetail("productsRevised", revised.ToString(System.Globalization.CultureInfo.InvariantCulture));
            TempData["Success"] = $"Revised {revised} prices in {model.Category}.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainValidationException exception)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "The request broke a business rule.", Detail = exception.Message });
        }
    }

    public Task RepopulateFormAsync(object model, CancellationToken ct)
    {
        if (model is ProductFormViewModel form && RouteData.Values["id"]?.ToString() is { } sku)
        {
            form.IsExisting = true;
            form.Sku = sku;
        }

        return Task.CompletedTask;
    }

    private static ProductRecordInput ToInput(ProductFormViewModel model) =>
        new(model.Sku, model.Name, model.Category, model.UnitPrice, model.TaxCategory);
}
