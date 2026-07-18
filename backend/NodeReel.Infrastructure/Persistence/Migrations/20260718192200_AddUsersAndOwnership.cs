using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NodeReel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "workflows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "pipeline_runs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "media_objects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_UserId",
                table: "workflows",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_pipeline_runs_UserId",
                table: "pipeline_runs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_media_objects_UserId",
                table: "media_objects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_workflows_UserId",
                table: "workflows");

            migrationBuilder.DropIndex(
                name: "IX_pipeline_runs_UserId",
                table: "pipeline_runs");

            migrationBuilder.DropIndex(
                name: "IX_media_objects_UserId",
                table: "media_objects");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "pipeline_runs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "media_objects");
        }
    }
}
