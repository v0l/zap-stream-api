using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class StreamTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdmissionCost",
                table: "Streams",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StreamTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserStreamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamTickets_Streams_UserStreamId",
                        column: x => x.UserStreamId,
                        principalTable: "Streams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamTickets_UserStreamId",
                table: "StreamTickets",
                column: "UserStreamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamTickets");

            migrationBuilder.DropColumn(
                name: "AdmissionCost",
                table: "Streams");
        }
    }
}
