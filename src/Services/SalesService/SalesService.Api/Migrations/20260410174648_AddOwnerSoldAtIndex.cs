using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerSoldAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sales_OwnerUsername_SoldAtUtc",
                table: "Sales",
                columns: new[] { "OwnerUsername", "SoldAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_OwnerUsername_SoldAtUtc",
                table: "Sales");
        }
    }
}
