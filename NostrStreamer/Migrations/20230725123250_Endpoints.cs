using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class Endpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EndpointId",
                table: "Streams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Endpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    App = table.Column<string>(type: "text", nullable: false),
                    Forward = table.Column<string>(type: "text", nullable: false),
                    Cost = table.Column<int>(type: "integer", nullable: false),
                    Capabilities = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Streams_EndpointId",
                table: "Streams",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_Endpoints_App",
                table: "Endpoints",
                column: "App",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Streams_Endpoints_EndpointId",
                table: "Streams",
                column: "EndpointId",
                principalTable: "Endpoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                "INSERT INTO public.\"Endpoints\"(\"Id\", \"Name\", \"App\", \"Forward\", \"Cost\", \"Capabilities\") VALUES(gen_random_uuid(), 'basic', 'basic', 'base.in.zap.stream', 1000, '{variant:source,dvr:source}');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Streams_Endpoints_EndpointId",
                table: "Streams");

            migrationBuilder.DropTable(
                name: "Endpoints");

            migrationBuilder.DropIndex(
                name: "IX_Streams_EndpointId",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "EndpointId",
                table: "Streams");
        }
    }
}
