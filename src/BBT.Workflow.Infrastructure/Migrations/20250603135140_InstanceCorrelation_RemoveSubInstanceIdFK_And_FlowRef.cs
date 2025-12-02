using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceCorrelation_RemoveSubInstanceIdFK_And_FlowRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstancesCorrelations_Instances_SubFlowInstanceId",
                schema: "public",
                table: "InstancesCorrelations");

            migrationBuilder.DropIndex(
                name: "IX_InstancesCorrelations_SubFlowInstanceId",
                schema: "public",
                table: "InstancesCorrelations");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowDomain",
                schema: "public",
                table: "InstancesCorrelations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowName",
                schema: "public",
                table: "InstancesCorrelations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubFlowVersion",
                schema: "public",
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
                schema: "public",
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "SubFlowName",
                schema: "public",
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "SubFlowVersion",
                schema: "public",
                table: "InstancesCorrelations");

            migrationBuilder.CreateIndex(
                name: "IX_InstancesCorrelations_SubFlowInstanceId",
                schema: "public",
                table: "InstancesCorrelations",
                column: "SubFlowInstanceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InstancesCorrelations_Instances_SubFlowInstanceId",
                schema: "public",
                table: "InstancesCorrelations",
                column: "SubFlowInstanceId",
                principalSchema: null,
                principalTable: "Instances",
                principalColumn: "Id");
        }
    }
}
