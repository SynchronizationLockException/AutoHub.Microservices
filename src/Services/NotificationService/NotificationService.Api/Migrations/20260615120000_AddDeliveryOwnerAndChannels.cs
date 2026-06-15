using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Api.Migrations;

public partial class AddDeliveryOwnerAndChannels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Channel",
            table: "NotificationDeliveries",
            type: "text",
            nullable: false,
            defaultValue: "log");

        migrationBuilder.AddColumn<string>(
            name: "Detail",
            table: "NotificationDeliveries",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OwnerUsername",
            table: "NotificationDeliveries",
            type: "text",
            nullable: false,
            defaultValue: "unknown");

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "NotificationDeliveries",
            type: "text",
            nullable: false,
            defaultValue: "Delivered");

        migrationBuilder.CreateIndex(
            name: "IX_NotificationDeliveries_OwnerUsername",
            table: "NotificationDeliveries",
            column: "OwnerUsername");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_NotificationDeliveries_OwnerUsername",
            table: "NotificationDeliveries");

        migrationBuilder.DropColumn(name: "Channel", table: "NotificationDeliveries");
        migrationBuilder.DropColumn(name: "Detail", table: "NotificationDeliveries");
        migrationBuilder.DropColumn(name: "OwnerUsername", table: "NotificationDeliveries");
        migrationBuilder.DropColumn(name: "Status", table: "NotificationDeliveries");
    }
}
