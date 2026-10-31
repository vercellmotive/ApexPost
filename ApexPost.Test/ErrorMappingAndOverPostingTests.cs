using System.Net;
using System.Text.RegularExpressions;
using ApexPost.Application.Interfaces;
using ApexPost.Application.Queries;
using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Test;

public sealed class ErrorMappingAndOverPostingTests : IClassFixture<ApexPostWebApplicationFactory>
{
    private static int _clientNumber = 100;
    private readonly ApexPostWebApplicationFactory _factory;

    public ErrorMappingAndOverPostingTests(ApexPostWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<Exception, int> Failures => new()
    {
        { new ConcurrencyConflictException("Changed by someone else."), StatusCodes.Status409Conflict },
        { new DuplicateValueException("Already exists."), StatusCodes.Status409Conflict },
        { new KeyNotFoundException("Missing."), StatusCodes.Status404NotFound },
        { new DomainValidationException("Broke a rule."), StatusCodes.Status400BadRequest },
        { new InvalidOperationException("Secret internal detail."), StatusCodes.Status500InternalServerError }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Exceptions_MapToProblemDetailsWithTheirStatus(Exception failure, int expectedStatus)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(_ => throw failure, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains($"\"status\":{expectedStatus}", body);
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("Secret internal detail", body);
    }

    [Fact]
    public async Task ConcurrencyConflictDuringSale_Returns409ProblemDetails()
    {
        using var factory = new ConflictOnSaveFactory();
        var client = CreateClient(factory);
        await LoginAsync(client, "ben@apexpost.com", "Apex-Ben-2026!");
        var token = await GetTokenAsync(client, "/Counter");

        var response = await client.PostAsync("/Counter/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CounterId"] = "C01",
            ["RetailItems[0].ProductSku"] = "SKU-001",
            ["RetailItems[0].Quantity"] = "1",
            ["PaymentMethod"] = "Cash",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var audit = await factory.Services.GetRequiredService<IAuditStore>().GetAllAsync();
        Assert.Contains(audit, entry => entry.Action == "Create" && entry.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task OverPostedSaleFields_HaveNoEffect()
    {
        var client = CreateClient(_factory);
        await LoginAsync(client, "cara@apexpost.com", "Apex-Cara-2026!");
        var token = await GetTokenAsync(client, "/Counter");
        var postedId = Guid.NewGuid();

        var response = await client.PostAsync("/Counter/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CounterId"] = "C01",
            ["RetailItems[0].ProductSku"] = "SKU-002",
            ["RetailItems[0].Quantity"] = "1",
            ["PaymentMethod"] = "Cash",
            ["Id"] = postedId.ToString(),
            ["Status"] = "Voided",
            ["ClerkStaffId"] = "STF001",
            ["BranchCode"] = "B002",
            ["CreatedBy"] = "attacker",
            ["CreatedAtUtc"] = "2000-01-01",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var created = _factory.Database.AllTransactions().Last();
        Assert.NotEqual(postedId, created.Id);
        Assert.Equal(TransactionStatus.Paid, created.Status);
        Assert.Equal("STF003", created.ClerkStaffId);
        Assert.Equal("B001", created.BranchCode.Value);
    }

    [Fact]
    public async Task OverPostedStaffFields_HaveNoEffect()
    {
        var client = CreateClient(_factory, "127.0.0.61");
        await LoginAsync(client, "olive@apexpost.com", "Apex-Olive-2026!");
        var token = await GetTokenAsync(client, "/Staff/Edit/STF016");
        var before = _factory.Database.StaffById("STF016");

        var response = await client.PostAsync("/Staff/Edit/STF016", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Parker Quinn",
            ["Email"] = before.Email,
            ["Role"] = before.Role.ToString(),
            ["HomeBranchCode"] = before.HomeBranchCode.Value,
            ["Id"] = "STF999",
            ["IsActive"] = "false",
            ["PasswordHash"] = "attacker-hash",
            ["CreatedBy"] = "attacker",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = _factory.Database.StaffById("STF016");
        Assert.True(after.IsActive);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.DoesNotContain(_factory.Database.AllStaff(), person => person.Id == "STF999");
    }

    [Fact]
    public async Task PriceRevision_IsSetBasedAndChangesOnlyItsCategory()
    {
        var client = CreateClient(_factory, "127.0.0.61");
        await LoginAsync(client, "olive@apexpost.com", "Apex-Olive-2026!");
        var token = await GetTokenAsync(client, "/Products");
        var packagingBefore = _factory.Database.ProductBySku("SKU-001").UnitPrice;

        var response = await client.PostAsync("/Products/RevisePrices", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Category"] = "Stationery",
            ["PercentChange"] = "10",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(packagingBefore, _factory.Database.ProductBySku("SKU-001").UnitPrice);
    }

    [Fact]
    public async Task ReportTotals_MatchThePersistedSale()
    {
        var client = CreateClient(_factory);
        await LoginAsync(client, "alice@apexpost.com", "Apex-Alice-2026!");
        var token = await GetTokenAsync(client, "/Services/MoneyOrder");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var scope = _factory.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportQueries>();
        var before = (await reports.GetDailyBranchSalesAsync("B001", today)).SingleOrDefault()?.ServicesTotal ?? 0m;

        var response = await client.PostAsync("/Services/MoneyOrder", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CounterId"] = "C01",
            ["Amount"] = "200",
            ["Sender"] = "Sender",
            ["Recipient"] = "Recipient",
            ["PaymentMethod"] = "Cash",
            ["__RequestVerificationToken"] = token
        }));

        // Alice is a manager at B001, so the sale goes through and only the fee counts as revenue.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = (await reports.GetDailyBranchSalesAsync("B001", today)).Single().ServicesTotal;
        Assert.Equal(before + 10m, after);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string? clientIp = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp ?? $"127.0.2.{Interlocked.Increment(ref _clientNumber) % 250}");
        return client;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var token = await GetTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> GetTokenAsync(HttpClient client, string path) =>
        Regex.Match(await (await client.GetAsync(path)).Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private sealed class ConflictOnSaveFactory : ApexPostWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IUnitOfWork, AlwaysConflictingUnitOfWork>());
        }

        private sealed class AlwaysConflictingUnitOfWork(Fakes.InMemoryPersistence persistence) : IUnitOfWork
        {
            public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
                throw new ConcurrencyConflictException("This record was changed by someone else at the same time.");

            public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default) => work(ct);

            public void DiscardPendingChanges() => persistence.DiscardPendingChanges();
        }
    }
}
