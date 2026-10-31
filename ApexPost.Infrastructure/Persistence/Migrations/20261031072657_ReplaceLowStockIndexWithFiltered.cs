using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLowStockIndexWithFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BranchStock_Branch_Quantity",
                table: "BranchStock");

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_LowStock",
                table: "BranchStock",
                columns: new[] { "BranchCode", "Sku" },
                filter: "[QuantityOnHand] <= 5")
                .Annotation("SqlServer:Include", new[] { "QuantityOnHand" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BranchStock_LowStock",
                table: "BranchStock");

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_Branch_Quantity",
                table: "BranchStock",
                columns: new[] { "BranchCode", "QuantityOnHand" })
                .Annotation("SqlServer:Include", new[] { "Sku" });
        }
    }
}
