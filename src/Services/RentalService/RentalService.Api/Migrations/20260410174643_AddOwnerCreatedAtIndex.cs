using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentalService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Rentals_OwnerUsername_CreatedAtUtc",
                table: "Rentals",
                columns: new[] { "OwnerUsername", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rentals_OwnerUsername_CreatedAtUtc",
                table: "Rentals");
        }
    }
}
