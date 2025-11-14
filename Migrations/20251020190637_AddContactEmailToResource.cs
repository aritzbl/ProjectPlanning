using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace projectplanning.Migrations
{
    /// <inheritdoc />
    public partial class AddContactEmailToResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Resources",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Resources");
        }
    }
}
