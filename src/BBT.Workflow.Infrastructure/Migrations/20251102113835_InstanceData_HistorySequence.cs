using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceData_HistorySequence : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public InstanceData_HistorySequence(IDbContextSchema schema)
        {
            _schema = schema;
        }
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId_Version",
                schema: _schema.SchemaName,
                table: "InstancesData");

            migrationBuilder.AddColumn<int>(
                name: "HistorySequence",
                schema: _schema.SchemaName,
                table: "InstancesData",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Update existing records with row numbers partitioned by InstanceId and ordered by EnteredAt
            migrationBuilder.Sql($@"
                UPDATE ""{_schema.SchemaName}"".""InstancesData"" AS target
                SET ""HistorySequence"" = source.rn
                FROM (
                    SELECT 
                        ""Id"",
                        ROW_NUMBER() OVER (PARTITION BY ""InstanceId"" ORDER BY ""EnteredAt"") -1 AS rn
                    FROM ""{_schema.SchemaName}"".""InstancesData""
                ) AS source
                WHERE target.""Id"" = source.""Id"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesData",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId_Version_HistorySequence",
                schema: _schema.SchemaName,
                table: "InstancesData",
                columns: new[] { "InstanceId", "Version", "HistorySequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesData");

            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId_Version_HistorySequence",
                schema: _schema.SchemaName,
                table: "InstancesData");

            migrationBuilder.DropColumn(
                name: "HistorySequence",
                schema: _schema.SchemaName,
                table: "InstancesData");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId_Version",
                schema: _schema.SchemaName,
                table: "InstancesData",
                columns: new[] { "InstanceId", "Version" },
                unique: true);
        }
    }
}
