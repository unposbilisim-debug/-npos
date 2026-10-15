using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UnAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackupAndRestore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BackupType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    BackupDirectory = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    TriggeredBy = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TriggeredByUserId = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupHistory_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestoreHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BackupFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TriggeredByUserId = table.Column<int>(type: "int", nullable: true),
                    SafetyBackupFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoreHistory_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_TriggeredByUserId",
                table: "BackupHistory",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupHistory_Type_Status_Completed",
                table: "BackupHistory",
                columns: new[] { "BackupType", "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreHistory_StartedAtUtc",
                table: "RestoreHistory",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreHistory_TriggeredByUserId",
                table: "RestoreHistory",
                column: "TriggeredByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupHistory");

            migrationBuilder.DropTable(
                name: "RestoreHistory");
        }
    }
}
