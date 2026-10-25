using System.Security.Claims;
using ApexPost.Application.Interfaces;
using ApexPost.Domain.Entities;
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
    private readonly IRepository<Staff> _staffRepository;
    private readonly IConfiguration _configuration;

    public AccountController(IRepository<Staff> staffRepository, IConfiguration configuration)
    {
        _staffRepository = staffRepository;
        _configuration = configuration;
    }

    [HttpGet("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/Account/Login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var staffs = await _staffRepository.GetAllAsync(ct);
        var staff = staffs.FirstOrDefault(x => x.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
        var demoPassword = _configuration["Authentication:DemoPassword"];
        if (staff is null || !staff.IsActive || string.IsNullOrEmpty(demoPassword)
            || !string.Equals(model.Password, demoPassword, StringComparison.Ordinal))
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

    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
