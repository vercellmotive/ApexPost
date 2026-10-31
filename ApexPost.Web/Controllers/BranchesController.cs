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
public sealed class BranchesController : Controller, IRepopulatesForm
{
    private readonly BranchManagementService _branches;
    private readonly IScreenQueries _queries;

    public BranchesController(BranchManagementService branches, IScreenQueries queries)
    {
        _branches = branches;
        _queries = queries;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _queries.GetBranchesAsync(ct));
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
        var branch = await _queries.GetBranchAsync(id, ct);
        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        return View("Form", new BranchFormViewModel
        {
            IsExisting = true,
            Code = branch.Code,
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
    public async Task<IActionResult> Edit([FromRoute] string id, BranchFormViewModel model, CancellationToken ct)
    {
        model.IsExisting = true;
        model.Code = id;
        try
        {
            await _branches.UpdateAsync(id, ToInput(model), ct);
            TempData["Success"] = "Branch updated.";
            return RedirectToAction(nameof(Index));
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
    public async Task<IActionResult> SetStatus([FromRoute] string id, BranchStatusRequest request, CancellationToken ct)
    {
        await _branches.SetActiveAsync(id, request.IsActive, ct);
        TempData["Success"] = request.IsActive ? "Branch reopened." : "Branch closed.";
        return RedirectToAction(nameof(Index));
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
