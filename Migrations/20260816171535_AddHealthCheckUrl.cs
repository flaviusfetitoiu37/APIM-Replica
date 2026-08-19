using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApimReplica.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthCheckUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HealthCheckUrl",
                table: "Apis",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthCheckUrl",
                table: "Apis");
        }
    }
}
