using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class InstanceData_IsLatest : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public InstanceData_IsLatest(IDbContextSchema schema)
        {
            _schema = schema;
        }
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLatest",
                schema: _schema.SchemaName,
                table: "InstancesData",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLatest",
                schema: _schema.SchemaName,
                table: "InstancesData");
        }
    }
}
