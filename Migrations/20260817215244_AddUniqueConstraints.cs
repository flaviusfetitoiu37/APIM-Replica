using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApimReplica.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchemaVersions_ApiId",
                table: "SchemaVersions");

            migrationBuilder.CreateIndex(
                name: "IX_SchemaVersions_ApiId_VersionNumber",
                table: "SchemaVersions",
                columns: new[] { "ApiId", "VersionNumber" },
                unique: true);

            // Expression index, so it cannot be declared with HasIndex in OnModelCreating.
            // Proxy route keys are lower-cased: "Petstore" and "PETSTORE" would both map
            // to /proxy/petstore and make YARP throw AmbiguousMatchException.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_Apis_Name_Lower"" ON ""Apis"" (lower(""Name""));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Apis_Name_Lower"";");

            migrationBuilder.DropIndex(
                name: "IX_SchemaVersions_ApiId_VersionNumber",
                table: "SchemaVersions");

            migrationBuilder.CreateIndex(
                name: "IX_SchemaVersions_ApiId",
                table: "SchemaVersions",
                column: "ApiId");
        }
    }
}
