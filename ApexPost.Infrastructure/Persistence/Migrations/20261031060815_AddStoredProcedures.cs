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
            migrationBuilder.Sql(SqlScripts.ReadForMigration("usp_AdjustStock"));
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
