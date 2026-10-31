using ApexPost.Web.Attributes;
using ApexPost.Application.Interfaces;
using ApexPost.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

[ApiController]
[AllowAnonymous]
public class IntegrationController : ControllerBase
{
    private readonly ICourierTrackingService _tracking;

    public IntegrationController(ICourierTrackingService tracking)
    {
        _tracking = tracking;
    }

    [HttpGet("/integration/parcel-status")]
    [ApiKeyRequired]
    [IpRestricted]
    [EnableCors("PartnerWidget")]
    [EnableRateLimiting("partner")]
    public async Task<IActionResult> ParcelStatus(
        string trackingId = "APX-2025-001",
        CancellationToken ct = default)
    {
        try
        {
            var status = await _tracking.GetStatusAsync(trackingId, ct);
            return Ok(status);
        }
        catch (CourierTrackingTimeoutException)
        {
            return Problem(
                title: "Courier tracking is temporarily unavailable.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }
}
