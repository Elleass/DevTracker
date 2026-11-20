using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueProjectNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId_Name",
                table: "Projects",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId_Name",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");
        }
    }
}
