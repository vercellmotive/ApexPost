using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApexPost.Web.Attributes;
using System.Security.Claims;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "BranchManagerOnly")]
public sealed class StockController : Controller
{
    private readonly InventoryService _inventory;

    public StockController(InventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpPost]
    [AuditedAction("StockAdjustment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(
        string productSku,
        int quantityDelta,
        StockAdjustmentReason reasonCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(productSku))
        {
            return BadRequest("A product SKU is required.");
        }

        var staffId = User.FindFirstValue("StaffId");
        var branchCode = User.FindFirstValue("BranchCode");
        if (staffId is null || branchCode is null)
        {
            return Forbid();
        }

        try
        {
            await _inventory.AdjustStockAsync(
                staffId,
                new BranchCode(branchCode),
                new Sku(productSku.Trim()),
                quantityDelta,
                reasonCode,
                ct);
            TempData["Success"] = $"Stock for {productSku} was adjusted.";
            return RedirectToAction("Index", "Catalog");
        }
        catch (UnauthorizedDomainActionException)
        {
            return Forbid();
        }
        catch (DomainValidationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
