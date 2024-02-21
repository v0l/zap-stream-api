using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class TrackDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                table: "Streams",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Length",
                table: "Streams");
        }
    }
}
