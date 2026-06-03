using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesService.Api.Migrations;

public partial class AddSaga : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            table: "Sales",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "ReservationId",
            table: "Sales",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "Sales",
            type: "text",
            nullable: false,
            defaultValue: "Active");

        migrationBuilder.CreateTable(
            name: "SagaInstances",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CorrelationId = table.Column<string>(type: "text", nullable: false),
                Type = table.Column<string>(type: "text", nullable: false),
                State = table.Column<string>(type: "text", nullable: false),
                StepDataJson = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SagaInstances", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SagaInstances_CorrelationId",
            table: "SagaInstances",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_SagaInstances_Type_State",
            table: "SagaInstances",
            columns: new[] { "Type", "State" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SagaInstances");
        migrationBuilder.DropColumn(name: "Status", table: "Sales");
        migrationBuilder.DropColumn(name: "ReservationId", table: "Sales");
        migrationBuilder.DropColumn(name: "CorrelationId", table: "Sales");
    }
}
