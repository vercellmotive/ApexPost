using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkloadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_BranchCode",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShippingRateId",
                table: "Shipments");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Branch_Sku_Time",
                table: "StockAdjustments",
                columns: new[] { "BranchCode", "Sku", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShippingRates_Zone_Level",
                table: "ShippingRates",
                columns: new[] { "ZoneId", "ServiceLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Rate",
                table: "Shipments",
                column: "ShippingRateId")
                .Annotation("SqlServer:Include", new[] { "TransactionLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyOrders_HighValue",
                table: "MoneyOrders",
                column: "Amount",
                filter: "[Amount] > 1000")
                .Annotation("SqlServer:Include", new[] { "Fee", "TransactionLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_Branch_Date",
                table: "CounterTransactions",
                columns: new[] { "BranchCode", "BusinessDate" })
                .Annotation("SqlServer:Include", new[] { "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_Branch_Quantity",
                table: "BranchStock",
                columns: new[] { "BranchCode", "QuantityOnHand" })
                .Annotation("SqlServer:Include", new[] { "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Branch_Time",
                table: "AuditEntries",
                columns: new[] { "BranchCode", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Staff_Time",
                table: "AuditEntries",
                columns: new[] { "StaffId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_Branch_Sku_Time",
                table: "StockAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_ShippingRates_Zone_Level",
                table: "ShippingRates");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_Rate",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_MoneyOrders_HighValue",
                table: "MoneyOrders");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_Branch_Date",
                table: "CounterTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BranchStock_Branch_Quantity",
                table: "BranchStock");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Branch_Time",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_Staff_Time",
                table: "AuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_BranchCode",
                table: "StockAdjustments",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShippingRateId",
                table: "Shipments",
                column: "ShippingRateId");
        }
    }
}
