using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceCorrelation_AddType : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public InstanceCorrelation_AddType(IDbContextSchema schema)
        {
            _schema = schema;
        }
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesCorrelations_ParentInstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowType",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                comment: "SubFlow Type: S (SubFlow - blocking) or P (SubProcess - non-blocking)");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesCorrelations_Performance",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                columns: new[] { "ParentInstanceId", "IsCompleted", "SubFlowType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstancesCorrelations_Performance",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "SubFlowType",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesCorrelations_ParentInstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                column: "ParentInstanceId");
        }
    }
}
