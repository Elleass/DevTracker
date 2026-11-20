using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedMinutes",
                table: "TaskItem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "TaskItem",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Estimate",
                table: "TaskItem",
                sql: "\"EstimatedMinutes\" IS NULL OR \"EstimatedMinutes\" BETWEEN 1 AND 525600");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Priority",
                table: "TaskItem",
                sql: "\"Priority\" BETWEEN 0 AND 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Estimate",
                table: "TaskItem");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Priority",
                table: "TaskItem");

            migrationBuilder.DropColumn(
                name: "EstimatedMinutes",
                table: "TaskItem");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "TaskItem");
        }
    }
}
