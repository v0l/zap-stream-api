using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class ForwardClientDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EdgeIp",
                table: "Streams",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ForwardClientId",
                table: "Streams",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EdgeIp",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "ForwardClientId",
                table: "Streams");
        }
    }
}
