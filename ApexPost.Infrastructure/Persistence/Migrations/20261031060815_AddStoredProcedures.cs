using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.ReadForMigration("usp_DailyBranchSales"));
            migrationBuilder.Sql(SqlScripts.ReadForMigration("usp_LowStockReport"));
            // The body as shipped: this runs before the audit columns exist, so it can't use the current file.
            migrationBuilder.Sql(SqlScripts.ReadForMigration("usp_AdjustStock_v1"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_AdjustStock;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LowStockReport;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_DailyBranchSales;");
        }
    }
}
