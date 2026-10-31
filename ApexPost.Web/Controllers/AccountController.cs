using System.Security.Claims;
using ApexPost.Application.Services;
using ApexPost.Web.Attributes;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly StaffAuthenticationService _authentication;

    public AccountController(StaffAuthenticationService authentication)
    {
        _authentication = authentication;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [RedisplayOnInvalid]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        var staff = await _authentication.AuthenticateAsync(model.Email, model.Password, ct);
        if (staff is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, staff.Name),
            new Claim(ClaimTypes.Email, staff.Email),
            new Claim(ClaimTypes.Role, staff.Role.ToString()),
            new Claim("StaffId", staff.Id),
            new Claim("Role", staff.Role.ToString()),
            new Claim("BranchCode", staff.HomeBranchCode.Value),
            new Claim(ClaimTypes.NameIdentifier, staff.Id)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(4)
        });

        var returnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        return Redirect(returnUrl ?? Url.Action(nameof(HomeController.Index), "Home") ?? "/");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
