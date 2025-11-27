using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIESTUR.Migrations
{
    /// <inheritdoc />
    public partial class v20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CalledByUserId",
                table: "Turns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Turns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByUserId",
                table: "Turns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Turns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ServedByUserId",
                table: "Turns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperatorDailyFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OperatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServedCount = table.Column<int>(type: "integer", nullable: false),
                    AvgServeToCompleteSec = table.Column<double>(type: "double precision", nullable: true),
                    AvgTotalLeadTimeSec = table.Column<double>(type: "double precision", nullable: true),
                    WindowMin = table.Column<int>(type: "integer", nullable: true),
                    WindowMax = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorDailyFacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TurnFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinalStatus = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WindowNumber = table.Column<int>(type: "integer", nullable: true),
                    OperatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ServedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SkippedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaitToCallSec = table.Column<int>(type: "integer", nullable: true),
                    CallToServeSec = table.Column<int>(type: "integer", nullable: true),
                    ServeToCompleteSec = table.Column<int>(type: "integer", nullable: true),
                    TotalLeadTimeSec = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnFacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Turns_Status_Kind_Number",
                table: "Turns",
                columns: new[] { "Status", "Kind", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnFacts_ServiceDate",
                table: "TurnFacts",
                column: "ServiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_TurnFacts_ServiceDate_OperatorUserId",
                table: "TurnFacts",
                columns: new[] { "ServiceDate", "OperatorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TurnFacts_ServiceDate_WindowNumber",
                table: "TurnFacts",
                columns: new[] { "ServiceDate", "WindowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorDailyFacts");

            migrationBuilder.DropTable(
                name: "TurnFacts");

            migrationBuilder.DropIndex(
                name: "IX_Turns_Status_Kind_Number",
                table: "Turns");

            migrationBuilder.DropColumn(
                name: "CalledByUserId",
                table: "Turns");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Turns");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "Turns");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Turns");

            migrationBuilder.DropColumn(
                name: "ServedByUserId",
                table: "Turns");
        }
    }
}
