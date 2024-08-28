using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class MigrateStreamInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentWarning",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Goal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StreamId",
                table: "Streams");

            migrationBuilder.AddColumn<string>(
                name: "ContentWarning",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Streams",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentWarning",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Goal",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Streams");

            migrationBuilder.AddColumn<string>(
                name: "ContentWarning",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreamId",
                table: "Streams",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
