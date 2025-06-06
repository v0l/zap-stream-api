﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NostrStreamer.Migrations
{
    /// <inheritdoc />
    public partial class PlannedUserStreams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Streams_Endpoints_EndpointId",
                table: "Streams");

            migrationBuilder.AlterColumn<Guid>(
                name: "EndpointId",
                table: "Streams",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Streams_Endpoints_EndpointId",
                table: "Streams",
                column: "EndpointId",
                principalTable: "Endpoints",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Streams_Endpoints_EndpointId",
                table: "Streams");

            migrationBuilder.AlterColumn<Guid>(
                name: "EndpointId",
                table: "Streams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Streams_Endpoints_EndpointId",
                table: "Streams",
                column: "EndpointId",
                principalTable: "Endpoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
