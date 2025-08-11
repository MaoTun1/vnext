using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceData_UniqueIndex : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public InstanceData_UniqueIndex(IDbContextSchema schema)
        {
            _schema = schema;
        }
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesData");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId_Version",
                schema: _schema.SchemaName,
                table: "InstancesData",
                columns: new[] { "InstanceId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId_Version",
                schema: _schema.SchemaName,
                table: "InstancesData");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesData",
                column: "InstanceId");
        }
    }
}
