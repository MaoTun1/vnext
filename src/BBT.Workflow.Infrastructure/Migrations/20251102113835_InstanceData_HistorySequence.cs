using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceData_HistorySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId_Version",
                schema: "public",
                table: "InstancesData");

            migrationBuilder.AddColumn<int>(
                name: "HistorySequence",
                schema: "public",
                table: "InstancesData",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Update existing records with row numbers partitioned by InstanceId and ordered by EnteredAt
            migrationBuilder.Sql($@"
                UPDATE ""InstancesData"" AS target
                SET ""HistorySequence"" = source.rn
                FROM (
                    SELECT 
                        ""Id"",
                        ROW_NUMBER() OVER (PARTITION BY ""InstanceId"" ORDER BY ""EnteredAt"") -1 AS rn
                    FROM ""InstancesData""
                ) AS source
                WHERE target.""Id"" = source.""Id"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId",
                schema: "public",
                table: "InstancesData",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId_Version_HistorySequence",
                schema: "public",
                table: "InstancesData",
                columns: new[] { "InstanceId", "Version", "HistorySequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId",
                schema: "public",
                table: "InstancesData");

            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId_Version_HistorySequence",
                schema: "public",
                table: "InstancesData");

            migrationBuilder.DropColumn(
                name: "HistorySequence",
                schema: "public",
                table: "InstancesData");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId_Version",
                schema: "public",
                table: "InstancesData",
                columns: new[] { "InstanceId", "Version" },
                unique: true);
        }
    }
}
