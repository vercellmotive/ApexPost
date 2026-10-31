using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Web.Attributes;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "AdministratorOnly")]
[IpRestricted]
public sealed class StaffController : Controller
{
    private readonly StaffManagementService _staff;

    public StaffController(StaffManagementService staff)
    {
        _staff = staff;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _staff.GetAllAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateBranchesAsync(ct);
        return View("Form", new StaffFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("StaffCreate")]
    public async Task<IActionResult> Create(StaffFormViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "A password is required for a new staff member.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateBranchesAsync(ct);
            return View("Form", model);
        }

        try
        {
            await _staff.CreateAsync(ToInput(model), ct);
            TempData["Success"] = "Staff member added.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateBranchesAsync(ct);
            return View("Form", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var person = await _staff.GetByIdAsync(id, ct);
        if (person is null)
        {
            return NotFound();
        }

        await PopulateBranchesAsync(ct);
        return View("Form", new StaffFormViewModel
        {
            Id = person.Id,
            Name = person.Name,
            Email = person.Email,
            Role = person.Role,
            HomeBranchCode = person.HomeBranchCode.Value
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("StaffUpdate")]
    public async Task<IActionResult> Edit(string id, StaffFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBranchesAsync(ct);
            model.Id = id;
            return View("Form", model);
        }

        try
        {
            await _staff.UpdateAsync(id, ToInput(model), ct);
            TempData["Success"] = "Staff member updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainValidationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await PopulateBranchesAsync(ct);
            return View("Form", model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuditedAction("StaffDeactivate")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct)
    {
        try
        {
            await _staff.DeactivateAsync(id, ct);
            TempData["Success"] = "Staff member deactivated.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task PopulateBranchesAsync(CancellationToken ct)
    {
        ViewBag.Branches = await _staff.GetBranchesAsync(ct);
    }

    private static StaffRecordInput ToInput(StaffFormViewModel model) =>
        new(model.Name, model.Email, model.Role, model.HomeBranchCode, model.Password);
}
