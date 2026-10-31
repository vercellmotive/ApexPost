using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using ApexPost.Application.Interfaces;
using ApexPost.Application.Queries;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Test;

// Set APEXPOST_TEST_SQLSERVER to a server connection string to run these. They create and drop a disposable
// database, and never touch ApexPostDb. Without the variable they are reported as skipped, not passed.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string Variable = "APEXPOST_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"{Variable} is not set, so no SQL Server is available.";
        }
    }
}

public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable);
        if (string.IsNullOrWhiteSpace(server))
        {
            return;
        }

        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = $"ApexPostTest_{Guid.NewGuid():N}" }.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await new DatabaseSeeder(db).SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (ConnectionString.Length > 0)
        {
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }
    }

    public ApexPostDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<ApexPostDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(interceptors)
            .Options);
}

public sealed class SqlServerIntegrationTests(SqlServerTestDatabase database) : IClassFixture<SqlServerTestDatabase>
{
    private static readonly BranchCode B001 = new("B001");
    private static readonly Sku Sku1 = new("SKU-001");

    [SqlServerFact]
    public async Task Seed_MeetsTheRequiredVolumes()
    {
        await using var db = database.CreateContext();

        Assert.True(await db.Branches.CountAsync() >= 5);
        Assert.True(await db.Staff.CountAsync() >= 20);
        Assert.True(await db.Products.CountAsync() >= 30);
        Assert.True(await db.ShippingZones.CountAsync() >= 10);
        Assert.True(await db.CounterTransactions.CountAsync() >= 200);
    }

    [SqlServerFact]
    public async Task Repository_RoundTripsATransactionThroughEveryTable()
    {
        var id = Guid.NewGuid();
        await using (var db = database.CreateContext())
        {
            var transaction = new Transaction { Id = id, BranchCode = B001, CounterId = "C01", ClerkStaffId = "STF002", BusinessDate = new DateOnly(2026, 10, 1) };
            transaction.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "Tape", LineItemType.RetailProduct, 1, new Money(10m), TaxCategory.StandardGst, Sku1));
            transaction.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "Shipping", LineItemType.ParcelShipping, 1, new Money(5m), TaxCategory.Exempt, Shipping: new ShippingDetails(true, Weight.FromKilograms(2m), new Dimensions(3, 4, 5), "Zone 3", ServiceLevel.Express)));
            transaction.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "ID", LineItemType.IdentityVerification, 1, Money.Zero, TaxCategory.Exempt, IdVerification: IdVerificationDetails.FromDocumentNumber(DocumentType.Passport, "A123456789", true)));
            transaction.Complete("Card", new Money(1000m));
            new CounterTransactionRepository(db).Add(transaction);
            await new UnitOfWork(db).SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var restored = await new CounterTransactionRepository(db).GetByIdAsync(id);

            Assert.NotNull(restored);
            Assert.Equal(TransactionStatus.Paid, restored.Status);
            Assert.Equal("Card", restored.PaymentMethod);
            Assert.Equal("Zone 3", restored.LineItems[1].Shipping!.DestinationZone);
            Assert.Equal("******6789", restored.LineItems[2].IdVerification!.MaskedDocNumber);
        }
    }

    [SqlServerFact]
    public async Task FailedSale_RollsBackStockAndTransactionTogether()
    {
        await using var db = database.CreateContext();
        var stock = await new StockRepository(db).GetAsync(B001, Sku1);
        var before = stock!.QuantityOnHand;
        stock.AddStock(1);
        var transaction = new Transaction { Id = Guid.NewGuid(), BranchCode = B001, CounterId = "NOT-A-COUNTER", ClerkStaffId = "STF002", BusinessDate = new DateOnly(2026, 10, 1) };
        transaction.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "Tape", LineItemType.RetailProduct, 1, new Money(10m), TaxCategory.Exempt, Sku1));
        transaction.Complete("Cash", new Money(1000m));
        new CounterTransactionRepository(db).Add(transaction);

        await Assert.ThrowsAsync<DbUpdateException>(() => new UnitOfWork(db).SaveChangesAsync());

        await using var verify = database.CreateContext();
        Assert.Equal(before, (await verify.BranchStock.AsNoTracking().SingleAsync(row => row.BranchCode == B001 && row.ProductSku == Sku1)).QuantityOnHand);
        Assert.False(await verify.CounterTransactions.AnyAsync(row => row.Id == transaction.Id));
    }

    [SqlServerFact]
    public async Task RowVersion_StopsTwoClerksSellingTheSameUnit()
    {
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var firstStock = await new StockRepository(first).GetAsync(B001, new Sku("SKU-002"));
        var secondStock = await new StockRepository(second).GetAsync(B001, new Sku("SKU-002"));

        firstStock!.AddStock(1);
        await new UnitOfWork(first).SaveChangesAsync();
        secondStock!.AddStock(1);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(second).SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task UniqueEmail_SurfacesAsDuplicateValue()
    {
        await using var db = database.CreateContext();
        db.Staff.Add(new Staff { Id = "STF900", Name = "Copy", Email = "alice@apexpost.com", Role = StaffRole.CounterClerk, HomeBranchCode = B001 });

        await Assert.ThrowsAsync<DuplicateValueException>(() => new UnitOfWork(db).SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task CheckConstraints_RejectNegativeStockAndUnmaskedDocuments()
    {
        await using var db = database.CreateContext();

        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE dbo.BranchStock SET QuantityOnHand = -1 WHERE BranchCode = {"B001"} AND Sku = {"SKU-001"}"));

        Assert.True(await db.IdentityVerifications.AnyAsync());
        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE dbo.IdentityVerifications SET MaskedDocumentNumber = {"A123456789"}"));
    }

    [SqlServerFact]
    public async Task AdjustStockProcedure_UpdatesStockAndWritesHistory()
    {
        await using var db = database.CreateContext();
        var before = (await db.BranchStock.AsNoTracking().SingleAsync(row => row.BranchCode == B001 && row.ProductSku == new Sku("SKU-003"))).QuantityOnHand;

        var after = await new StockAdjustmentCommand(db).AdjustAsync(B001, new Sku("SKU-003"), 2, StockAdjustmentReason.Replenishment, "STF001");

        Assert.Equal(before + 2, after);
        Assert.True(await db.StockAdjustments.AnyAsync(row => row.BranchCode == B001 && row.Sku == new Sku("SKU-003") && row.Delta == 2 && row.QuantityAfter == after));
    }

    [SqlServerFact]
    public async Task AdjustStockProcedure_RejectsOverdrawAndUnknownStockWithoutChangingAnything()
    {
        await using var db = database.CreateContext();
        var command = new StockAdjustmentCommand(db);
        var history = await db.StockAdjustments.CountAsync();

        await Assert.ThrowsAsync<DomainValidationException>(() => command.AdjustAsync(B001, Sku1, -1000000, StockAdjustmentReason.Damage, "STF001"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => command.AdjustAsync(B001, new Sku("SKU-404"), 1, StockAdjustmentReason.Damage, "STF001"));

        Assert.Equal(history, await db.StockAdjustments.CountAsync());
    }

    [SqlServerFact]
    public async Task ReportProcedures_MatchTotalsCalculatedFromTheTables()
    {
        await using var db = database.CreateContext();
        var reports = new ReportQueries(db);
        var date = (await reports.GetLatestSalesDateAsync("B001"))!.Value;

        var row = Assert.Single(await reports.GetDailyBranchSalesAsync("B001", date));

        var expectedRetail = await db.TransactionLines
            .Where(line => line.LineType == LineItemType.RetailProduct
                && line.Transaction!.BranchCode == B001
                && line.Transaction.BusinessDate == date
                && line.Transaction.Status == TransactionStatus.Paid)
            .SumAsync(line => line.LineTotal);
        Assert.Equal(expectedRetail, row.RetailTotal);

        var lowStock = await reports.GetLowStockAsync("B001", 5);
        Assert.All(lowStock, item => Assert.True(item.QuantityOnHand <= 5));
        Assert.All(await reports.GetLowStockAsync(null, 1000000), item => Assert.False(string.IsNullOrEmpty(item.ProductName)));
    }

    [SqlServerFact]
    public async Task PriceRevision_UsesOneSetBasedStatement()
    {
        var counter = new CommandCounter();
        await using var db = database.CreateContext(counter);
        var before = await db.Products.AsNoTracking().Where(product => product.CategoryId == 3).Select(product => product.UnitPrice).ToListAsync();
        counter.Reset();

        var revised = await new PriceRevisionCommand(db).ReviseByCategoryAsync(ProductCategories.OfficeSupplies, 10m);

        Assert.Equal(before.Count, revised);
        Assert.Equal(1, counter.Commands);
    }

    [SqlServerFact]
    public async Task ScreenQueries_DoNotIssueOneQueryPerRow()
    {
        var counter = new CommandCounter();
        await using var db = database.CreateContext(counter);
        var queries = new ScreenQueries(db);

        counter.Reset();
        var transactions = await queries.GetBranchTransactionsAsync("B001", 200);
        Assert.True(transactions.Count > 1);
        Assert.Equal(1, counter.Commands);

        counter.Reset();
        var catalogue = await queries.GetBranchCatalogueAsync("B001");
        Assert.True(catalogue.Count >= 30);
        Assert.Equal(1, counter.Commands);

        counter.Reset();
        await queries.GetBranchesAsync();
        Assert.True(counter.Commands <= 2, $"Expected at most 2 commands but saw {counter.Commands}.");
    }

    [SqlServerFact]
    public async Task AuditStore_WritesToAuditEntriesIndependently()
    {
        using var factory = new SqlServerWebFactory(database.ConnectionString);
        var store = (IAuditStore)factory.Services.GetService(typeof(IAuditStore))!;
        var id = Guid.NewGuid();

        await store.AppendAsync(new AuditEntry(id, "corr", "STF001", "BranchManager", "B001", "CounterController", "Void", "{}", 302, 5, DateTimeOffset.UtcNow));

        Assert.Contains(await store.GetAllAsync(), entry => entry.Id == id);
    }

    [SqlServerFact]
    public async Task Mvc_SalePersistsVoidNeedsManagerAndErrorsAreProblemDetails()
    {
        using var factory = new SqlServerWebFactory(database.ConnectionString);
        await using (var db = database.CreateContext())
        {
            await db.Branches.ExecuteUpdateAsync(setters => setters
                .SetProperty(branch => branch.TimeZoneId, "UTC")
                .SetProperty(branch => branch.OpenTime, TimeSpan.Zero)
                .SetProperty(branch => branch.CloseTime, new TimeSpan(23, 59, 59)));
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.3.1");
        await LoginAsync(client, "ben@apexpost.com", "Apex-Ben-2026!");
        var token = await TokenAsync(client, "/Counter");

        var sale = await client.PostAsync("/Counter/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CounterId"] = "C01",
            ["RetailItems[0].ProductSku"] = "SKU-004",
            ["RetailItems[0].Quantity"] = "1",
            ["PaymentMethod"] = "Card",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, sale.StatusCode);

        await using var verify = database.CreateContext();
        var saved = await verify.CounterTransactions.AsNoTracking().OrderByDescending(row => row.OccurredAtUtc).FirstAsync();
        Assert.Equal(TransactionStatus.Paid, saved.Status);

        var denied = await client.PostAsync("/Counter/Void", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["transactionId"] = saved.Id.ToString(),
            ["reason"] = "Clerk attempt",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains(await factory.ReadAuditAsync(), entry => entry.Action == "Create" && entry.StatusCode == 302);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var token = await TokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> TokenAsync(HttpClient client, string path) =>
        Regex.Match(await (await client.GetAsync(path)).Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private sealed class SqlServerWebFactory(string connectionString) : WebApplicationFactory<Program>
    {
        public Task<IReadOnlyList<AuditEntry>> ReadAuditAsync() =>
            ((IAuditStore)Services.GetService(typeof(IAuditStore))!).GetAllAsync();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApexPostDb"] = connectionString,
                ["Database:SeedOnStartup"] = "false"
            }));
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _commands;

        public int Commands => Volatile.Read(ref _commands);

        public void Reset() => Interlocked.Exchange(ref _commands, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commands);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _commands);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
