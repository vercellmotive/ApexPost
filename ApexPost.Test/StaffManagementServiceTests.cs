using ApexPost.Application.Interfaces;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Test;

public sealed class StaffManagementServiceTests
{
    [Fact]
    public async Task CreateNormalizesValuesAndAssignsNextStaffId()
    {
        var existing = CreateStaff("STF009", "existing@example.test");
        var staff = new MemoryRepository<Staff>([existing]);
        var service = CreateService(staff);

        var created = await service.CreateAsync(new StaffRecordInput(
            "  New Clerk ",
            " new@example.test ",
            StaffRole.CounterClerk,
            " B001 "));

        Assert.Equal("STF010", created.Id);
        Assert.Equal("New Clerk", created.Name);
        Assert.Equal("new@example.test", created.Email);
        Assert.Equal(new BranchCode("B001"), created.HomeBranchCode);
    }

    [Fact]
    public async Task CreateRejectsDuplicateEmailIgnoringCase()
    {
        var service = CreateService(new MemoryRepository<Staff>(
            [CreateStaff("STF001", "clerk@example.test")]));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateAsync(new StaffRecordInput(
                "Another Clerk",
                "CLERK@example.test",
                StaffRole.CounterClerk,
                "B001")));
    }

    [Fact]
    public async Task ConcurrentCreatesDoNotAssignDuplicateIdsOrEmails()
    {
        var staff = new MemoryRepository<Staff>([CreateStaff("STF001", "existing@example.test")]);
        var service = CreateService(staff);
        var input = new StaffRecordInput("New Clerk", "new@example.test", StaffRole.CounterClerk, "B001");

        var outcomes = await Task.WhenAll(
            CreateAndCaptureAsync(service, input),
            CreateAndCaptureAsync(service, input));

        Assert.Single(outcomes.OfType<Staff>());
        Assert.Single(outcomes.OfType<DomainValidationException>());
        Assert.Equal(2, (await staff.GetAllAsync()).Select(person => person.Id).Distinct().Count());
    }

    [Fact]
    public async Task UpdatePreservesInactiveStatusAndDeactivateRejectsUnknownStaff()
    {
        var existing = CreateStaff("STF001", "old@example.test");
        existing.IsActive = false;
        var staff = new MemoryRepository<Staff>([existing]);
        var service = CreateService(staff);

        var updated = await service.UpdateAsync(
            "STF001",
            new StaffRecordInput("Updated Clerk", "updated@example.test", StaffRole.Auditor, "B001"));

        Assert.False(updated.IsActive);
        Assert.Equal("Updated Clerk", updated.Name);
        Assert.Equal(StaffRole.Auditor, updated.Role);
        await service.DeactivateAsync("STF001");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeactivateAsync("STF404"));
    }

    [Fact]
    public async Task CreateRejectsUnknownBranchAndUndefinedRole()
    {
        var service = CreateService(new MemoryRepository<Staff>());

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateAsync(new StaffRecordInput(
                "Clerk",
                "clerk@example.test",
                StaffRole.CounterClerk,
                "B404")));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CreateAsync(new StaffRecordInput(
                "Clerk",
                "clerk@example.test",
                (StaffRole)999,
                "B001")));
    }

    private static async Task<object> CreateAndCaptureAsync(
        StaffManagementService service,
        StaffRecordInput input)
    {
        try
        {
            return await service.CreateAsync(input);
        }
        catch (DomainValidationException exception)
        {
            return exception;
        }
    }

    private static StaffManagementService CreateService(MemoryRepository<Staff> staff) =>
        new(staff, new MemoryRepository<Branch>(
        [
            new Branch
            {
                Id = new BranchCode("B001"),
                Name = "Test Branch",
                Region = "NSW",
                OpenTime = TimeSpan.Zero,
                CloseTime = TimeSpan.FromHours(23)
            }
        ]));

    private static Staff CreateStaff(string id, string email) => new()
    {
        Id = id,
        Name = "Existing Staff",
        Email = email,
        Role = StaffRole.CounterClerk,
        HomeBranchCode = new BranchCode("B001")
    };

    private sealed class MemoryRepository<T>(IEnumerable<T>? initial = null) : IRepository<T>
        where T : class
    {
        private readonly List<T> _items = initial?.ToList() ?? [];

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<T>>(_items.ToList());
        }

        public Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException($"{typeof(T).Name} does not define an Id.");
            return Task.FromResult(_items.FirstOrDefault(item => Equals(idProperty.GetValue(item), id)));
        }

        public Task<T?> UpdateAsync(object id, Action<T> update, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException($"{typeof(T).Name} does not define an Id.");
            var item = _items.FirstOrDefault(value => Equals(idProperty.GetValue(value), id));
            if (item is not null)
            {
                update(item);
            }

            return Task.FromResult(item);
        }

        public Task SaveAsync(T entity, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var idProperty = typeof(T).GetProperty("Id");
            var entityId = idProperty?.GetValue(entity);
            var existingIndex = idProperty is null
                ? -1
                : _items.FindIndex(item => Equals(idProperty.GetValue(item), entityId));
            if (existingIndex < 0)
            {
                _items.Add(entity);
            }
            else
            {
                _items[existingIndex] = entity;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(object id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException($"{typeof(T).Name} does not define an Id.");
            _items.RemoveAll(item => Equals(idProperty.GetValue(item), id));
            return Task.CompletedTask;
        }
    }
}
