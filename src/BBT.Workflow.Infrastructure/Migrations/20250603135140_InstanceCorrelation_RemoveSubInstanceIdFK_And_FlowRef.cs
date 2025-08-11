using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceCorrelation_RemoveSubInstanceIdFK_And_FlowRef : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public InstanceCorrelation_RemoveSubInstanceIdFK_And_FlowRef(IDbContextSchema schema)
        {
            _schema = schema;
        }
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstancesCorrelations_Instances_SubFlowInstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropIndex(
                name: "IX_InstancesCorrelations_SubFlowInstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowDomain",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowName",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowVersion",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubFlowDomain",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "SubFlowName",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "SubFlowVersion",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesCorrelations_SubFlowInstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                column: "SubFlowInstanceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InstancesCorrelations_Instances_SubFlowInstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                column: "SubFlowInstanceId",
                principalSchema: _schema.SchemaName,
                principalTable: "Instances",
                principalColumn: "Id");
        }
    }
}
