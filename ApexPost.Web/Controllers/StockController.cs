using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using ApexPost.Web.Attributes;
using ApexPost.Web.Filters;
using ApexPost.Web.Models;
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
    public async Task<IActionResult> Adjust(StockAdjustmentRequest request, CancellationToken ct)
    {
        HttpContext.AddAuditDetail("productSku", request.ProductSku);
        HttpContext.AddAuditDetail("quantityDelta", request.QuantityDelta.ToString(CultureInfo.InvariantCulture));
        HttpContext.AddAuditDetail("reasonCode", request.ReasonCode.ToString());

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
                new Sku(request.ProductSku),
                request.QuantityDelta,
                request.ReasonCode,
                ct);
            TempData["Success"] = $"Stock for {request.ProductSku} was adjusted.";
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
