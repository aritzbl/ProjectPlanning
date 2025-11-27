using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectplanning.Migrations
{
    /// <inheritdoc />
    public partial class AddBonitaCaseIdProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BonitaCaseId",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BonitaCaseId",
                table: "Projects");
        }
    }
}
