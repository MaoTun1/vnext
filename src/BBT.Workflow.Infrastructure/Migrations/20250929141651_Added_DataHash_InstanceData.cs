using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class Added_DataHash_InstanceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataHash",
                schema: "public",
                table: "InstancesData",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "99914b932bd37a50b983c5e7c90ae93b");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataHash",
                schema: "public",
                table: "InstancesData");
        }
    }
}
