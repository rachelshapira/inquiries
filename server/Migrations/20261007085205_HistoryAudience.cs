using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Api.Migrations
{
    /// <inheritdoc />
    public partial class HistoryAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "History",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicNote",
                table: "History",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "History");

            migrationBuilder.DropColumn(
                name: "PublicNote",
                table: "History");
        }
    }
}
