using System;
using System.Text.Json;
using BBT.Workflow.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <inheritdoc />
    public partial class Added_BackgroundJobAndInboxOutbox : Migration
    {
        private readonly IDbContextSchema _schema;
        
        public Added_BackgroundJobAndInboxOutbox(IDbContextSchema schema)
        {
            _schema = schema;
        }
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                schema: _schema.SchemaName,
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "MetaData",
                schema: _schema.SchemaName,
                table: "Instances");

            migrationBuilder.DropColumn(
                name: "ExpressionValue",
                schema: _schema.SchemaName,
                table: "InstanceJobs");

            migrationBuilder.DropColumn(
                name: "Payload",
                schema: _schema.SchemaName,
                table: "InstanceJobs");

            migrationBuilder.RenameColumn(
                name: "IsTriggered",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                newName: "IsActive");

            migrationBuilder.AddColumn<Guid>(
                name: "InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                schema: _schema.SchemaName,
                table: "Instances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "JobName",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(125)",
                oldMaxLength: 125);

            
            migrationBuilder.AddColumn<Guid>(
                name: "JobId_tmp",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()"
            );
            
            migrationBuilder.Sql($@"
                UPDATE ""{_schema.SchemaName}"".""InstanceJobs""
                SET ""JobId_tmp"" = CASE
                    WHEN ""JobId"" ~* '^[0-9a-fA-F-]{36}$' THEN ""JobId""::uuid
                    ELSE gen_random_uuid()
                END
            ");
            
            migrationBuilder.DropColumn(
                name: "JobId",
                schema: _schema.SchemaName,
                table: "InstanceJobs"
            );
            
            migrationBuilder.RenameColumn(
                name: "JobId_tmp",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                newName: "JobId"
            );

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                schema: _schema.SchemaName,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HandlerName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    JobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpressionValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HandledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    CreatedByBehalfOf = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ModifiedByBehalfOf = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: _schema.SchemaName,
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventData = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HandledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NextRetryTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: _schema.SchemaName,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventData = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstancesCorrelations_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_HandlerName_Status",
                schema: _schema.SchemaName,
                table: "BackgroundJobs",
                columns: new[] { "HandlerName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_JobName",
                schema: _schema.SchemaName,
                table: "BackgroundJobs",
                column: "JobName");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Processing",
                schema: _schema.SchemaName,
                table: "BackgroundJobs",
                columns: new[] { "Status", "HandledTime" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Cleanup",
                schema: _schema.SchemaName,
                table: "InboxMessages",
                columns: new[] { "Status", "HandledTime" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Processing",
                schema: _schema.SchemaName,
                table: "InboxMessages",
                columns: new[] { "Status", "NextRetryTime", "RetryCount" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Cleanup",
                schema: _schema.SchemaName,
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Processing",
                schema: _schema.SchemaName,
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "NextRetryAt", "RetryCount" });

            migrationBuilder.AddForeignKey(
                name: "FK_InstancesCorrelations_Instances_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations",
                column: "InstanceId",
                principalSchema: "public",
                principalTable: "Instances",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstancesCorrelations_Instances_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: _schema.SchemaName);

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: _schema.SchemaName);

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: _schema.SchemaName);

            migrationBuilder.DropIndex(
                name: "IX_InstancesCorrelations_InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                schema: _schema.SchemaName,
                table: "InstancesCorrelations");

            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                schema: _schema.SchemaName,
                table: "Instances");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                newName: "IsTriggered");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                schema: _schema.SchemaName,
                table: "Instances",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MetaData",
                schema: _schema.SchemaName,
                table: "Instances",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "JobName",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                type: "character varying(125)",
                maxLength: 125,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "JobId",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ExpressionValue",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                schema: _schema.SchemaName,
                table: "InstanceJobs",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }
    }
}
