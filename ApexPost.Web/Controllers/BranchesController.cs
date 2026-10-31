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
public sealed class BranchesController : Controller, IRepopulatesForm
{
    private readonly BranchManagementService _branches;

    public BranchesController(BranchManagementService branches)
    {
        _branches = branches;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _branches.GetAllAsync(ct));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Form", new BranchFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Form")]
    [AuditedAction("BranchCreate")]
    public async Task<IActionResult> Create(BranchFormViewModel model, CancellationToken ct)
    {
        try
        {
            await _branches.CreateAsync(ToInput(model), ct);
            TempData["Success"] = "Branch added.";
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
        var branch = await _branches.GetByCodeAsync(id, ct);
        if (branch is null)
        {
            return NotFound();
        }

        return View("Form", new BranchFormViewModel
        {
            IsExisting = true,
            Code = branch.Id.Value,
            Name = branch.Name,
            Region = branch.Region,
            OpenTime = branch.OpenTime,
            CloseTime = branch.CloseTime,
            TimeZoneId = branch.TimeZoneId,
            CounterTerminals = string.Join(", ", branch.CounterTerminals)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RedisplayOnInvalid("Form")]
    [AuditedAction("BranchUpdate")]
    public async Task<IActionResult> Edit(string id, BranchFormViewModel model, CancellationToken ct)
    {
        model.IsExisting = true;
        model.Code = id;
        try
        {
            await _branches.UpdateAsync(id, ToInput(model), ct);
            TempData["Success"] = "Branch updated.";
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
    [AuditedAction("BranchStatusChange")]
    public async Task<IActionResult> SetStatus(string id, bool isActive, CancellationToken ct)
    {
        try
        {
            await _branches.SetActiveAsync(id, isActive, ct);
            TempData["Success"] = isActive ? "Branch reopened." : "Branch closed.";
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public Task RepopulateFormAsync(object model, CancellationToken ct)
    {
        if (model is BranchFormViewModel form && RouteData.Values["id"]?.ToString() is { } code)
        {
            form.IsExisting = true;
            form.Code = code;
        }

        return Task.CompletedTask;
    }

    private static BranchRecordInput ToInput(BranchFormViewModel model) =>
        new(
            model.Code,
            model.Name,
            model.Region,
            model.OpenTime,
            model.CloseTime,
            model.TimeZoneId,
            model.CounterTerminals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
