using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archive_operations",
                columns: table => new
                {
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    archive_type = table.Column<int>(type: "integer", nullable: false),
                    operation_type = table.Column<int>(type: "integer", nullable: false),
                    group_name = table.Column<string>(type: "text", nullable: false),
                    total_number_of_messages = table.Column<int>(type: "integer", nullable: false),
                    number_of_messages_processed = table.Column<int>(type: "integer", nullable: false),
                    number_of_batches = table.Column<int>(type: "integer", nullable: false),
                    current_batch = table.Column<int>(type: "integer", nullable: false),
                    started = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    initiated_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    initiated_by_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    operation_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archive_operations", x => new { x.request_id, x.archive_type, x.operation_type });
                });

            migrationBuilder.CreateTable(
                name: "custom_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_check_id = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    originating_endpoint_name = table.Column<string>(type: "text", nullable: false),
                    originating_endpoint_host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    originating_endpoint_host = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_checks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archive_operations_started",
                table: "archive_operations",
                column: "started");

            migrationBuilder.CreateIndex(
                name: "ix_custom_checks_reported_at",
                table: "custom_checks",
                column: "reported_at");

            migrationBuilder.CreateIndex(
                name: "ix_custom_checks_status_reported_at",
                table: "custom_checks",
                columns: new[] { "status", "reported_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archive_operations");

            migrationBuilder.DropTable(
                name: "custom_checks");
        }
    }
}
