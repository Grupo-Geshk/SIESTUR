using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIESTUR.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedTurnFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkerSessions_UserId",
                table: "WorkerSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkerSessions_WindowId",
                table: "WorkerSessions");

            migrationBuilder.DropIndex(
                name: "IX_Turns_WindowId",
                table: "Turns");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Turns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSessions_UserId_EndedAt",
                table: "WorkerSessions",
                columns: new[] { "UserId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSessions_WindowId_EndedAt",
                table: "WorkerSessions",
                columns: new[] { "WindowId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_Position",
                table: "Videos",
                column: "Position");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turns_CreatedAt",
                table: "Turns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Turns_Status_Number_CreatedAt",
                table: "Turns",
                columns: new[] { "Status", "Number", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Turns_WindowId_Status",
                table: "Turns",
                columns: new[] { "WindowId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkerSessions_UserId_EndedAt",
                table: "WorkerSessions");

            migrationBuilder.DropIndex(
                name: "IX_WorkerSessions_WindowId_EndedAt",
                table: "WorkerSessions");

            migrationBuilder.DropIndex(
                name: "IX_Videos_Position",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Turns_CreatedAt",
                table: "Turns");

            migrationBuilder.DropIndex(
                name: "IX_Turns_Status_Number_CreatedAt",
                table: "Turns");

            migrationBuilder.DropIndex(
                name: "IX_Turns_WindowId_Status",
                table: "Turns");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Turns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSessions_UserId",
                table: "WorkerSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSessions_WindowId",
                table: "WorkerSessions",
                column: "WindowId");

            migrationBuilder.CreateIndex(
                name: "IX_Turns_WindowId",
                table: "Turns",
                column: "WindowId");
        }
    }
}
