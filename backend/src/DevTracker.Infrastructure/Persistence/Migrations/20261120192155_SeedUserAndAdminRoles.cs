using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedUserAndAdminRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("c4b56538-9dc8-4e95-a9f0-e853ca5bd574"), "devtracker-admin-role-v1", "ADMIN", "ADMIN" },
                    { new Guid("d11f9eb6-0e68-40b5-bb49-d40fda7c7311"), "devtracker-user-role-v1", "USER", "USER" }
                });

            // Existing accounts receive the same baseline role as new registrations.
            migrationBuilder.Sql("""
                INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT "Id", 'd11f9eb6-0e68-40b5-bb49-d40fda7c7311'::uuid
                FROM "AspNetUsers"
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("c4b56538-9dc8-4e95-a9f0-e853ca5bd574"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("d11f9eb6-0e68-40b5-bb49-d40fda7c7311"));
        }
    }
}
