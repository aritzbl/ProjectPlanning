using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectplanning.Migrations
{
    /// <inheritdoc />
    public partial class AddBonitaFieldsToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActualEndDate",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessInstanceId",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessStatus",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualEndDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProcessInstanceId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProcessStatus",
                table: "Projects");
        }
    }
}
