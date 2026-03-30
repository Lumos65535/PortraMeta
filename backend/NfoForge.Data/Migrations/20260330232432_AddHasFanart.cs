using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NfoForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHasFanart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasFanart",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasFanart",
                table: "VideoFiles");
        }
    }
}
