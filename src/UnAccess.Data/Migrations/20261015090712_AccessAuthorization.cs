using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UnAccess.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccessAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ControllerId = table.Column<int>(type: "int", nullable: true),
                    DoorId = table.Column<int>(type: "int", nullable: false),
                    PersonnelId = table.Column<int>(type: "int", nullable: true),
                    CardId = table.Column<int>(type: "int", nullable: true),
                    CardNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RawData = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessEvents_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessEvents_Controllers_ControllerId",
                        column: x => x.ControllerId,
                        principalTable: "Controllers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessEvents_Doors_DoorId",
                        column: x => x.DoorId,
                        principalTable: "Doors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessEvents_Personnel_PersonnelId",
                        column: x => x.PersonnelId,
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonnelAccessGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonnelId = table.Column<int>(type: "int", nullable: false),
                    AccessGroupId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonnelAccessGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonnelAccessGroups_AccessGroups_AccessGroupId",
                        column: x => x.AccessGroupId,
                        principalTable: "AccessGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelAccessGroups_Personnel_PersonnelId",
                        column: x => x.PersonnelId,
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessGroupDoors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessGroupId = table.Column<int>(type: "int", nullable: false),
                    DoorId = table.Column<int>(type: "int", nullable: false),
                    TimeScheduleId = table.Column<int>(type: "int", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGroupDoors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessGroupDoors_AccessGroups_AccessGroupId",
                        column: x => x.AccessGroupId,
                        principalTable: "AccessGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGroupDoors_Doors_DoorId",
                        column: x => x.DoorId,
                        principalTable: "Doors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGroupDoors_TimeSchedules_TimeScheduleId",
                        column: x => x.TimeScheduleId,
                        principalTable: "TimeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessGroupZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessGroupId = table.Column<int>(type: "int", nullable: false),
                    ZoneId = table.Column<int>(type: "int", nullable: false),
                    TimeScheduleId = table.Column<int>(type: "int", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGroupZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessGroupZones_AccessGroups_AccessGroupId",
                        column: x => x.AccessGroupId,
                        principalTable: "AccessGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGroupZones_TimeSchedules_TimeScheduleId",
                        column: x => x.TimeScheduleId,
                        principalTable: "TimeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGroupZones_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonnelAccessOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonnelId = table.Column<int>(type: "int", nullable: false),
                    ZoneId = table.Column<int>(type: "int", nullable: true),
                    DoorId = table.Column<int>(type: "int", nullable: true),
                    TimeScheduleId = table.Column<int>(type: "int", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonnelAccessOverrides", x => x.Id);
                    table.CheckConstraint("CK_PersonnelAccessOverrides_Target", "([ZoneId] IS NOT NULL AND [DoorId] IS NULL) OR ([ZoneId] IS NULL AND [DoorId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PersonnelAccessOverrides_Doors_DoorId",
                        column: x => x.DoorId,
                        principalTable: "Doors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelAccessOverrides_Personnel_PersonnelId",
                        column: x => x.PersonnelId,
                        principalTable: "Personnel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelAccessOverrides_TimeSchedules_TimeScheduleId",
                        column: x => x.TimeScheduleId,
                        principalTable: "TimeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelAccessOverrides_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeScheduleDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeScheduleId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeScheduleDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeScheduleDetails_TimeSchedules_TimeScheduleId",
                        column: x => x.TimeScheduleId,
                        principalTable: "TimeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_Card_OccurredAtUtc",
                table: "AccessEvents",
                columns: new[] { "CardNumber", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_CardId",
                table: "AccessEvents",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_ControllerId",
                table: "AccessEvents",
                column: "ControllerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_Door_OccurredAtUtc",
                table: "AccessEvents",
                columns: new[] { "DoorId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_OccurredAtUtc",
                table: "AccessEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEvents_Personnel_OccurredAtUtc",
                table: "AccessEvents",
                columns: new[] { "PersonnelId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_AccessEvents_EventId",
                table: "AccessEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroupDoors_DoorId",
                table: "AccessGroupDoors",
                column: "DoorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroupDoors_TimeScheduleId",
                table: "AccessGroupDoors",
                column: "TimeScheduleId");

            migrationBuilder.CreateIndex(
                name: "UX_AccessGroupDoors_Group_Door",
                table: "AccessGroupDoors",
                columns: new[] { "AccessGroupId", "DoorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AccessGroups_Code",
                table: "AccessGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroupZones_TimeScheduleId",
                table: "AccessGroupZones",
                column: "TimeScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroupZones_ZoneId",
                table: "AccessGroupZones",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "UX_AccessGroupZones_Group_Zone",
                table: "AccessGroupZones",
                columns: new[] { "AccessGroupId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessGroups_AccessGroupId",
                table: "PersonnelAccessGroups",
                column: "AccessGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessGroups_PersonnelId",
                table: "PersonnelAccessGroups",
                column: "PersonnelId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonnelAccessGroups_Person_Group",
                table: "PersonnelAccessGroups",
                columns: new[] { "PersonnelId", "AccessGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessOverrides_DoorId",
                table: "PersonnelAccessOverrides",
                column: "DoorId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessOverrides_Personnel_Door",
                table: "PersonnelAccessOverrides",
                columns: new[] { "PersonnelId", "DoorId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessOverrides_Personnel_Zone",
                table: "PersonnelAccessOverrides",
                columns: new[] { "PersonnelId", "ZoneId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessOverrides_TimeScheduleId",
                table: "PersonnelAccessOverrides",
                column: "TimeScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelAccessOverrides_ZoneId",
                table: "PersonnelAccessOverrides",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeScheduleDetails_TimeScheduleId",
                table: "TimeScheduleDetails",
                column: "TimeScheduleId");

            migrationBuilder.CreateIndex(
                name: "UX_TimeSchedules_Code",
                table: "TimeSchedules",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessEvents");

            migrationBuilder.DropTable(
                name: "AccessGroupDoors");

            migrationBuilder.DropTable(
                name: "AccessGroupZones");

            migrationBuilder.DropTable(
                name: "PersonnelAccessGroups");

            migrationBuilder.DropTable(
                name: "PersonnelAccessOverrides");

            migrationBuilder.DropTable(
                name: "TimeScheduleDetails");

            migrationBuilder.DropTable(
                name: "AccessGroups");

            migrationBuilder.DropTable(
                name: "TimeSchedules");
        }
    }
}
