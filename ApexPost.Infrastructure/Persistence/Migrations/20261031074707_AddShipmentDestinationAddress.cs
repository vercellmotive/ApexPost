using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentDestinationAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationCity",
                table: "Shipments",
                type: "nvarchar(60)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationCountry",
                table: "Shipments",
                type: "nvarchar(60)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationPostalCode",
                table: "Shipments",
                type: "varchar(12)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationState",
                table: "Shipments",
                type: "nvarchar(30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationStreet",
                table: "Shipments",
                type: "nvarchar(120)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationCity",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DestinationCountry",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DestinationPostalCode",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DestinationState",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DestinationStreet",
                table: "Shipments");
        }
    }
}
