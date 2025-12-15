using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BBT.Workflow.Migrations
{
    /// <summary>
    /// Migration to add VersionNo column and PostgreSQL trigger for automatic version management.
    /// This ensures concurrency safety for InstanceData by using advisory locks and auto-incrementing version numbers.
    /// </summary>
    /// <remarks>
    /// This migration performs the following steps:
    /// <list type="number">
    ///     <item><description>Drops obsolete unique index on (InstanceId, Version, HistorySequence)</description></item>
    ///     <item><description>Adds VersionNo (bigint) column with default value 0</description></item>
    ///     <item><description>Makes IsLatest non-nullable with default value false</description></item>
    ///     <item><description>Backfills existing records with proper VersionNo and IsLatest values</description></item>
    ///     <item><description>Creates unique index on (InstanceId, VersionNo)</description></item>
    ///     <item><description>Creates partial unique index on InstanceId WHERE IsLatest = true</description></item>
    ///     <item><description>Creates PostgreSQL function for version and latest management</description></item>
    ///     <item><description>Creates BEFORE INSERT trigger to auto-set VersionNo and IsLatest</description></item>
    /// </list>
    /// </remarks>
    public partial class AddInstanceDataVersioningTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0) Drop obsolete unique index on (InstanceId, Version, HistorySequence)
            // This is being replaced by (InstanceId, VersionNo) for better concurrency control
            migrationBuilder.DropIndex(
                name: "IX_InstancesData_InstanceId_Version_HistorySequence",
                schema: "public",
                table: "InstancesData");

            // 0.1) Alter Version column to support longer version strings (180 chars)
            // This accommodates semantic versioning with build metadata: MAJOR.MINOR.PATCH-pkg.PKG_VERSION+PKG_NAME
            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "public",
                table: "InstancesData",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            // 1) Add VersionNo column
            migrationBuilder.AddColumn<long>(
                name: "VersionNo",
                schema: "public",
                table: "InstancesData",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // 2) Make IsLatest non-nullable (it was nullable before)
            // First update any null values to false
            migrationBuilder.Sql(@"
                UPDATE ""InstancesData""
                SET ""IsLatest"" = FALSE
                WHERE ""IsLatest"" IS NULL;
            ");

            migrationBuilder.AlterColumn<bool>(
                name: "IsLatest",
                schema: "public",
                table: "InstancesData",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            // 3) Backfill existing records with VersionNo and IsLatest
            // VersionNo: Sequential per InstanceId ordered by EnteredAt, Id
            // IsLatest: Only the latest record per InstanceId should be true
            migrationBuilder.Sql(@"
WITH ordered AS (
    SELECT 
        ""Id"",
        ROW_NUMBER() OVER (
            PARTITION BY ""InstanceId""
            ORDER BY ""EnteredAt"", ""Id""
        ) AS ""VersionNo"",
        ROW_NUMBER() OVER (
            PARTITION BY ""InstanceId""
            ORDER BY ""EnteredAt"" DESC, ""Id"" DESC
        ) AS ""RevOrder""
    FROM ""InstancesData""
)
UPDATE ""InstancesData"" t
SET 
    ""VersionNo"" = o.""VersionNo"",
    ""IsLatest"" = CASE WHEN o.""RevOrder"" = 1 THEN TRUE ELSE FALSE END
FROM ordered o
WHERE t.""Id"" = o.""Id"";
            ");

            // 4) Create unique index on (InstanceId, VersionNo)
            migrationBuilder.CreateIndex(
                name: "UX_InstancesData_Instance_VersionNo",
                schema: "public",
                table: "InstancesData",
                columns: new[] { "InstanceId", "VersionNo" },
                unique: true);

            // 5) Create partial unique index for IsLatest = true
            migrationBuilder.CreateIndex(
                name: "UX_InstancesData_Instance_IsLatest",
                schema: "public",
                table: "InstancesData",
                column: "InstanceId",
                unique: true,
                filter: "\"IsLatest\" = true");

            // 6) Create PostgreSQL function for version and latest management
            // Uses advisory lock to prevent race conditions during concurrent inserts
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION set_instance_data_version_and_latest()
RETURNS trigger AS $$
DECLARE
    next_version_no bigint;
BEGIN
    -- Instance-level advisory lock (transaction-scoped, automatically released on commit/rollback)
    PERFORM pg_advisory_xact_lock(hashtext(NEW.""InstanceId""::text));

    -- Get next version number for this instance
    SELECT COALESCE(MAX(""VersionNo""), 0) + 1
      INTO next_version_no
      FROM ""InstancesData""
     WHERE ""InstanceId"" = NEW.""InstanceId"";

    NEW.""VersionNo"" := next_version_no;

    -- Mark previous latest as not latest
    UPDATE ""InstancesData""
       SET ""IsLatest"" = FALSE
     WHERE ""InstanceId"" = NEW.""InstanceId""
       AND ""IsLatest"" = TRUE;

    -- Mark this record as latest
    NEW.""IsLatest"" := TRUE;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
            ");

            // 7) Create BEFORE INSERT trigger
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_instancesdata_set_version_and_latest
BEFORE INSERT ON ""InstancesData""
FOR EACH ROW
EXECUTE FUNCTION set_instance_data_version_and_latest();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop trigger
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_instancesdata_set_version_and_latest ON ""InstancesData"";
            ");

            // Drop function
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS set_instance_data_version_and_latest();
            ");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "UX_InstancesData_Instance_VersionNo",
                schema: "public",
                table: "InstancesData");

            migrationBuilder.DropIndex(
                name: "UX_InstancesData_Instance_IsLatest",
                schema: "public",
                table: "InstancesData");

            // Revert IsLatest to nullable
            migrationBuilder.AlterColumn<bool>(
                name: "IsLatest",
                schema: "public",
                table: "InstancesData",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: false,
                oldDefaultValue: false);

            // Drop VersionNo column
            migrationBuilder.DropColumn(
                name: "VersionNo",
                schema: "public",
                table: "InstancesData");

            // Revert Version column back to 20 chars
            migrationBuilder.AlterColumn<string>(
                name: "Version",
                schema: "public",
                table: "InstancesData",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180);

            // Recreate the original unique index on (InstanceId, Version, HistorySequence)
            migrationBuilder.CreateIndex(
                name: "IX_InstancesData_InstanceId_Version_HistorySequence",
                schema: "public",
                table: "InstancesData",
                columns: new[] { "InstanceId", "Version", "HistorySequence" },
                unique: true);
        }
    }
}

