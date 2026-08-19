using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApimReplica.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HealthStatus",
                table: "Apis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedAt",
                table: "Apis",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastLatencyMs",
                table: "Apis",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthStatus",
                table: "Apis");

            migrationBuilder.DropColumn(
                name: "LastCheckedAt",
                table: "Apis");

            migrationBuilder.DropColumn(
                name: "LastLatencyMs",
                table: "Apis");
        }
    }
}
