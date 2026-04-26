using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortraMeta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaInfoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioCodec",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BitRate",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "VideoFiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrameRate",
                table: "VideoFiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoCodec",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioCodec",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "BitRate",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "FrameRate",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "VideoCodec",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "VideoFiles");
        }
    }
}
