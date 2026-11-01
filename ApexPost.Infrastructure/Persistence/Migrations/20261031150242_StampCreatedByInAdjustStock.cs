using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StampCreatedByInAdjustStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The body as shipped: ValidateChangedByInAdjustStock replaces it with the current file.
            migrationBuilder.Sql(SqlScripts.ReadForMigration("usp_AdjustStock_v2"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.ReadForMigration("usp_AdjustStock_v1"));
        }
    }
}
