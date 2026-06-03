using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarCatalogService.Api.Migrations;

public partial class AddReservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<uint>(
            name: "RowVersion",
            table: "Cars",
            type: "integer",
            rowVersion: true,
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.CreateTable(
            name: "Reservations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CarId = table.Column<Guid>(type: "uuid", nullable: false),
                Purpose = table.Column<string>(type: "text", nullable: false),
                HolderReference = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reservations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_CarId_Status",
            table: "Reservations",
            columns: new[] { "CarId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Reservations_HolderReference",
            table: "Reservations",
            column: "HolderReference");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Reservations");
        migrationBuilder.DropColumn(name: "RowVersion", table: "Cars");
    }
}
