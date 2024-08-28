using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class StreamKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StreamKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserPubkey = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Expires = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StreamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamKeys_Streams_StreamId",
                        column: x => x.StreamId,
                        principalTable: "Streams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamKeys_Users_UserPubkey",
                        column: x => x.UserPubkey,
                        principalTable: "Users",
                        principalColumn: "PubKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamKeys_StreamId",
                table: "StreamKeys",
                column: "StreamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamKeys_UserPubkey",
                table: "StreamKeys",
                column: "UserPubkey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamKeys");
        }
    }
}
