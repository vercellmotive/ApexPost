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
public sealed class StaffController : Controller, IRepopulatesForm
{
    private readonly StaffManagementService _staff;
    private readonly IScreenQueries _queries;

    public StaffController(StaffManagementService staff, IScreenQueries queries)
    {
        _staff = staff;
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _queries.GetStaffAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateBranchesAsync(ct);
        return View("Form", new StaffFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Form")]
    [AuditedAction("StaffCreate")]
    public async Task<IActionResult> Create(StaffFormViewModel model, CancellationToken ct)
    {

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
        var person = await _queries.GetStaffMemberAsync(id, ct);
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
            HomeBranchCode = person.HomeBranchCode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Form")]
    [AuditedAction("StaffUpdate")]
    public async Task<IActionResult> Edit([FromRoute] string id, StaffFormViewModel model, CancellationToken ct)
    {
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
    public async Task<IActionResult> Deactivate([FromRoute] string id, CancellationToken ct)
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

    public async Task RepopulateFormAsync(object model, CancellationToken ct)
    {
        await PopulateBranchesAsync(ct);
        if (model is StaffFormViewModel form)
        {
            form.Id = RouteData.Values["id"]?.ToString();
        }
    }

    private async Task PopulateBranchesAsync(CancellationToken ct)
    {
        ViewBag.Branches = await _queries.GetBranchesAsync(ct);
    }

    private static StaffRecordInput ToInput(StaffFormViewModel model) =>
        new(model.Name, model.Email, model.Role, model.HomeBranchCode, model.Password);
}
