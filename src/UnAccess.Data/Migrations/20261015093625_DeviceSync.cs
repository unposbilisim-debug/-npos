using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UnAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeviceSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionStatus",
                table: "Controllers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSimulation",
                table: "Controllers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Controllers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessfulSyncAtUtc",
                table: "Controllers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncAttemptAtUtc",
                table: "Controllers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceSyncQueue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ControllerId = table.Column<int>(type: "int", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSyncQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceSyncQueue_Controllers_ControllerId",
                        column: x => x.ControllerId,
                        principalTable: "Controllers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Doors_Controller_Channel_Active",
                table: "Doors",
                columns: new[] { "ControllerId", "ChannelNumber" },
                unique: true,
                filter: "[IsActive] = 1 AND [ControllerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSyncQueue_Claim",
                table: "DeviceSyncQueue",
                columns: new[] { "Status", "NextAttemptAtUtc", "Priority", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSyncQueue_ControllerId",
                table: "DeviceSyncQueue",
                column: "ControllerId");

            migrationBuilder.CreateIndex(
                name: "UX_DeviceSyncQueue_OperationId",
                table: "DeviceSyncQueue",
                column: "OperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceSyncQueue");

            migrationBuilder.DropIndex(
                name: "UX_Doors_Controller_Channel_Active",
                table: "Doors");

            migrationBuilder.DropColumn(
                name: "ConnectionStatus",
                table: "Controllers");

            migrationBuilder.DropColumn(
                name: "IsSimulation",
                table: "Controllers");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Controllers");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulSyncAtUtc",
                table: "Controllers");

            migrationBuilder.DropColumn(
                name: "LastSyncAttemptAtUtc",
                table: "Controllers");
        }
    }
}
