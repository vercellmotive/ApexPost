using System.Text.Json;
using ApexPost.Application.Interfaces;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using ApexPost.Web.Attributes;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ShippingController : Controller
{
    private readonly ICourierRateService _courierRates;

    public ShippingController(ICourierRateService courierRates)
    {
        _courierRates = courierRates;
    }

    [HttpGet]
    public IActionResult ShippingLabel()
    {
        var preview = TempData["ShippingLabelPreview"] as string;
        var model = preview is null
            ? new ShippingLabelViewModel()
            : JsonSerializer.Deserialize<ShippingLabelViewModel>(preview)
                ?? throw new JsonException("The saved shipping label preview is invalid.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid]
    public async Task<IActionResult> ShippingLabel(ShippingLabelViewModel model, CancellationToken ct)
    {
        try
        {
            var details = new ShippingDetails(
                model.IsParcel,
                Weight.FromKilograms(model.WeightKg),
                new Dimensions(model.LengthCm, model.WidthCm, model.HeightCm),
                model.DestinationZone,
                model.ServiceLevel);
            model.Price = (await _courierRates.GetEstimatedCourierRateAsync(details, ct)).Amount;
            TempData["ShippingLabelPreview"] = JsonSerializer.Serialize(model);
            return RedirectToAction(nameof(ShippingLabel));
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }
}
