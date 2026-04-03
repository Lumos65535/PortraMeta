using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortraMeta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedNfoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountriesJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditsJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateAdded",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectorsJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenresJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mpaa",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outline",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Premiered",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RatingsJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Runtime",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetName",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SortTitle",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Top250",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UniqueIdsJson",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserRating",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountriesJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "CreditsJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "DirectorsJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "GenresJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Mpaa",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Outline",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Premiered",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "RatingsJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Runtime",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "SetName",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "SortTitle",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "Top250",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "UniqueIdsJson",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "UserRating",
                table: "VideoFiles");
        }
    }
}
