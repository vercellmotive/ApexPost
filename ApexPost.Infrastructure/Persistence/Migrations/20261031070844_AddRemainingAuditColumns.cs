using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemainingAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "StockAdjustments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "StockAdjustments",
                type: "varchar(50)",
                nullable: true,
                defaultValue: "system");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "StockAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Roles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Roles",
                type: "varchar(50)",
                nullable: true,
                defaultValue: "system");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "Roles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ProductCategories",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProductCategories",
                type: "varchar(50)",
                nullable: true,
                defaultValue: "system");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "ProductCategories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AuditEntries",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AuditEntries",
                type: "varchar(50)",
                nullable: true,
                defaultValue: "system");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAtUtc",
                table: "AuditEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ProductCategories",
                keyColumn: "Id",
                keyValue: 1,
                column: "ModifiedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "ProductCategories",
                keyColumn: "Id",
                keyValue: 2,
                column: "ModifiedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "ProductCategories",
                keyColumn: "Id",
                keyValue: 3,
                column: "ModifiedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 0,
                column: "ModifiedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "ModifiedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "ModifiedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "ModifiedAtUtc",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "AuditEntries");
        }
    }
}
