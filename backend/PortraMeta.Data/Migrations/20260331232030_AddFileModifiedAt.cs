using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortraMeta.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileModifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FileModifiedAt",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileModifiedAt",
                table: "VideoFiles");
        }
    }
}
