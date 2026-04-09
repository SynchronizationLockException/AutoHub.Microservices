using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Api.Migrations;

public partial class RefreshTokenHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DELETE FROM "RefreshTokens";""");

        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens");

        migrationBuilder.DropColumn(
            name: "Token",
            table: "RefreshTokens");

        migrationBuilder.AddColumn<byte[]>(
            name: "TokenHash",
            table: "RefreshTokens",
            type: "bytea",
            nullable: false);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_TokenHash",
            table: "RefreshTokens",
            column: "TokenHash",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_TokenHash",
            table: "RefreshTokens");

        migrationBuilder.DropColumn(
            name: "TokenHash",
            table: "RefreshTokens");

        migrationBuilder.AddColumn<string>(
            name: "Token",
            table: "RefreshTokens",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token",
            unique: true);
    }
}
