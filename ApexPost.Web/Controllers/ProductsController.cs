using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Web.Attributes;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "AdministratorOnly")]
[IpRestricted]
public sealed class ProductsController : Controller
{
    private readonly ProductManagementService _products;

    public ProductsController(ProductManagementService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _products.GetAllAsync(ct));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Form", new ProductFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("ProductCreate")]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

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
        var product = await _products.GetBySkuAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        return View("Form", new ProductFormViewModel
        {
            IsExisting = true,
            Sku = product.Id.Value,
            Name = product.Name,
            Category = product.Category,
            UnitPrice = product.UnitPrice.Amount,
            TaxCategory = product.TaxCategory
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("ProductUpdate")]
    public async Task<IActionResult> Edit(string id, ProductFormViewModel model, CancellationToken ct)
    {
        model.IsExisting = true;
        model.Sku = id;
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

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

    private static ProductRecordInput ToInput(ProductFormViewModel model) =>
        new(model.Sku, model.Name, model.Category, model.UnitPrice, model.TaxCategory);
}
