using System.Text.Json;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

// Fictional demo data only. Reference tables come from migrations; this adds the operational rows once.
public sealed class DatabaseSeeder(ApexPostDbContext db)
{
    public const int MinimumHistoricalTransactions = 200;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> SeedAsync(CancellationToken ct = default)
    {
        if (await db.Branches.AnyAsync(ct))
        {
            return false;
        }

        var branches = await ReadAsync<Branch>("branch.json", ct);
        var staff = await ReadAsync<Staff>("staff.json", ct);
        var products = await ReadAsync<Product>("product.json", ct);
        var stock = await ReadAsync<BranchStock>("branchstock.json", ct);
        var seeded = await ReadAsync<Transaction>("transaction.json", ct);

        // Several saves make up one seed, so a failure part-way must leave the database empty, not half-seeded.
        await new UnitOfWork(db).ExecuteInTransactionAsync(async token =>
        {
            // The execution strategy may retry this delegate, so each attempt starts from a clean tracker.
            db.ChangeTracker.Clear();
            db.Branches.AddRange(branches.Select(PersistenceMapper.ToRecord));
            db.Products.AddRange(products.Select(PersistenceMapper.ToRecord));
            await db.SaveChangesAsync(token);
            db.Staff.AddRange(staff);
            db.BranchStock.AddRange(stock);
            await db.SaveChangesAsync(token);

            var transactions = BuildHistory(branches, staff, products, seeded, DateTimeOffset.UtcNow);
            db.CounterTransactions.AddRange(transactions.Select(PersistenceMapper.ToRecord));
            await db.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    // Deterministic, so every install gets comparable report totals.
    public static IReadOnlyList<Transaction> BuildHistory(
        IReadOnlyList<Branch> branches,
        IReadOnlyList<Staff> staff,
        IReadOnlyList<Product> products,
        IReadOnlyList<Transaction> seeded,
        DateTimeOffset now)
    {
        var random = new Random(20260601);
        var branchByCode = branches.ToDictionary(branch => branch.Id);
        var history = seeded.Select(transaction => Transaction.Rehydrate(
            transaction.Id,
            transaction.BranchCode,
            transaction.CounterId,
            transaction.ClerkStaffId,
            transaction.Status,
            transaction.LineItems,
            transaction.PaymentMethod,
            transaction.CreatedAt,
            branchByCode[transaction.BranchCode].LocalDate(transaction.CreatedAt),
            transaction.VoidReason,
            transaction.VoidedByManagerId,
            transaction.InventoryRestocked)).ToList();
        var paymentMethods = new[] { "Cash", "Card", "EFTPOS" };
        var levels = Enum.GetValues<ServiceLevel>();

        while (history.Count < MinimumHistoricalTransactions + 20)
        {
            var branch = branches[random.Next(branches.Count)];
            var clerks = staff.Where(person => person.HomeBranchCode == branch.Id && person.Role == StaffRole.CounterClerk).ToList();
            var occurredAt = now.AddDays(-random.Next(1, 90)).AddMinutes(-random.Next(0, 480));
            var lines = new List<TransactionLineItem>();
            foreach (var _ in Enumerable.Range(0, random.Next(1, 4)))
            {
                var product = products[random.Next(products.Count)];
                var existing = lines.FindIndex(line => line.ProductSku == product.Id);
                if (existing < 0)
                {
                    lines.Add(new TransactionLineItem(Guid.NewGuid(), product.Name, LineItemType.RetailProduct, random.Next(1, 4), product.UnitPrice, product.TaxCategory, product.Id));
                }
            }

            if (random.Next(3) == 0)
            {
                var level = levels[random.Next(levels.Length)];
                lines.Add(new TransactionLineItem(
                    Guid.NewGuid(),
                    $"Shipping: {level}",
                    LineItemType.ParcelShipping,
                    1,
                    new Money(12.50m + random.Next(0, 15)),
                    TaxCategory.Exempt,
                    Shipping: new ShippingDetails(true, Weight.FromKilograms(1m + random.Next(0, 1)), new Dimensions(30, 20, 10), $"Zone {random.Next(1, 11)}", level)));
            }

            if (random.Next(5) == 0)
            {
                var amount = new Money(random.Next(0, 4) == 0 ? 1500m : 50m + random.Next(0, 400));
                var fee = new Money(10m);
                lines.Add(new TransactionLineItem(Guid.NewGuid(), "Money order", LineItemType.MoneyOrder, 1, amount + fee, TaxCategory.Exempt, MoneyOrder: new MoneyOrderDetails(amount, "Demo Sender", "Demo Recipient", fee)));
                if (amount.Amount > 1000m)
                {
                    lines.Add(new TransactionLineItem(
                        Guid.NewGuid(),
                        "Identity verification",
                        LineItemType.IdentityVerification,
                        1,
                        Money.Zero,
                        TaxCategory.Exempt,
                        IdVerification: IdVerificationDetails.FromDocumentNumber(DocumentType.Passport, $"DEMO{random.Next(100000, 999999)}", true)));
                }
            }

            var isReversed = random.Next(20) == 0;
            var managers = staff.Where(person => person.HomeBranchCode == branch.Id && person.Role == StaffRole.BranchManager).ToList();
            var status = isReversed && managers.Count > 0
                ? (random.Next(2) == 0 ? TransactionStatus.Voided : TransactionStatus.Refunded)
                : TransactionStatus.Paid;
            history.Add(Transaction.Rehydrate(
                Guid.NewGuid(),
                branch.Id,
                branch.CounterTerminals[random.Next(branch.CounterTerminals.Count)],
                clerks.Count > 0 ? clerks[random.Next(clerks.Count)].Id : staff.First(person => person.HomeBranchCode == branch.Id).Id,
                status,
                lines,
                paymentMethods[random.Next(paymentMethods.Length)],
                occurredAt,
                branch.LocalDate(occurredAt),
                status == TransactionStatus.Paid ? null : "Demo reversal",
                status == TransactionStatus.Paid ? null : managers[0].Id,
                status != TransactionStatus.Paid));
        }

        return history;
    }

    private static async Task<List<T>> ReadAsync<T>(string fileName, CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException($"Seed file {fileName} is empty.");
    }
}
