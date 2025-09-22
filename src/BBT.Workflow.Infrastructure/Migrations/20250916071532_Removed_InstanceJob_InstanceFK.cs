using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class Removed_InstanceJob_InstanceFK : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public Removed_InstanceJob_InstanceFK(IDbContextSchema schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstanceJobs_Instances_InstanceId",
                schema: _schema.SchemaName,
                table: "InstanceJobs");

            migrationBuilder.DropIndex(
                name: "IX_InstanceJobs_InstanceId",
                schema: _schema.SchemaName,
                table: "InstanceJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InstanceJobs_InstanceId",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                column: "InstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstanceJobs_Instances_InstanceId",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                column: "InstanceId",
                principalSchema: "public",
                principalTable: "Instances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
