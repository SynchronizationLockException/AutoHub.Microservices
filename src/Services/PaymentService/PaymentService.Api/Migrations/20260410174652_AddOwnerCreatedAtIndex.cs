using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_OwnerUsername_CreatedAtUtc",
                table: "Payments",
                columns: new[] { "OwnerUsername", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OwnerUsername_CreatedAtUtc",
                table: "Payments");
        }
    }
}
