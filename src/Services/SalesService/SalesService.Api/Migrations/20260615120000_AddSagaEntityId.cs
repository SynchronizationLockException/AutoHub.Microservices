using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesService.Api.Migrations;

public partial class AddSagaEntityId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "EntityId",
            table: "SagaInstances",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SagaInstances_Type_EntityId",
            table: "SagaInstances",
            columns: new[] { "Type", "EntityId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SagaInstances_Type_EntityId",
            table: "SagaInstances");

        migrationBuilder.DropColumn(
            name: "EntityId",
            table: "SagaInstances");
    }
}
