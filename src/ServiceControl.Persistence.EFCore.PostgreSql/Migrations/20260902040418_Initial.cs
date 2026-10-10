using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
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

            migrationBuilder.CreateTable(
                name: "endpoint_settings",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    track_instances = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_endpoint_settings", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "event_log_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    raised_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    related_to = table.Column<List<string>>(type: "text[]", nullable: false),
                    category = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    event_type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_log_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "external_integration_dispatch_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dispatch_context_type_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    dispatch_context_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_integration_dispatch_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "failed_error_imports",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    message_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<byte[]>(type: "bytea", nullable: false),
                    body_stored_externally = table.Column<bool>(type: "boolean", nullable: false),
                    exception_info = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_error_imports", x => x.unique_message_id);
                });

            migrationBuilder.CreateTable(
                name: "failed_message_edits",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edit_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_message_edits", x => x.unique_message_id);
                });

            migrationBuilder.CreateTable(
                name: "failed_message_retries",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retry_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_message_retries", x => x.unique_message_id);
                });

            migrationBuilder.CreateTable(
                name: "failed_messages",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    number_of_processing_attempts = table.Column<int>(type: "integer", nullable: false),
                    first_time_of_failure = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_time_of_failure = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_attempted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    message_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    message_type = table.Column<string>(type: "text", nullable: true),
                    time_sent = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    conversation_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    sending_endpoint_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    sending_endpoint_host_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sending_endpoint_host = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    receiving_endpoint_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    receiving_endpoint_host_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receiving_endpoint_host = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    exception_type = table.Column<string>(type: "text", nullable: true),
                    exception_message = table.Column<string>(type: "text", nullable: true),
                    is_system_message = table.Column<bool>(type: "boolean", nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: true),
                    body_stored_externally = table.Column<bool>(type: "boolean", nullable: false),
                    body_size = table.Column<int>(type: "integer", nullable: false),
                    body_content_type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    failing_endpoint_address = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_messages", x => x.unique_message_id);
                });

            migrationBuilder.CreateTable(
                name: "group_comments",
                columns: table => new
                {
                    group_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_comments", x => x.group_id);
                });

            migrationBuilder.CreateTable(
                name: "historic_retry_operations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    request_id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    retry_type = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    originator = table.Column<string>(type: "text", nullable: true),
                    failed = table.Column<bool>(type: "boolean", nullable: false),
                    number_of_messages_processed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historic_retry_operations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "known_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    monitored = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_known_endpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "licensing_endpoints",
                columns: table => new
                {
                    normalized_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    throughput_source = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sanitized_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    normalized_sanitized_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    user_indicator = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    scope = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    endpoint_indicators = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_licensing_endpoints", x => new { x.normalized_name, x.throughput_source });
                });

            migrationBuilder.CreateTable(
                name: "message_redirects",
                columns: table => new
                {
                    from_physical_address = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    to_physical_address = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_redirects", x => x.from_physical_address);
                });

            migrationBuilder.CreateTable(
                name: "retry_batch_now_forwarding",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    retry_batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retry_batch_now_forwarding", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retry_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_session_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    request_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    retry_type = table.Column<int>(type: "integer", nullable: false),
                    initial_batch_size = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    staging_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    context = table.Column<string>(type: "text", nullable: true),
                    originator = table.Column<string>(type: "text", nullable: true),
                    classifier = table.Column<string>(type: "text", nullable: true),
                    initiated_by_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    initiated_by_name = table.Column<string>(type: "text", nullable: true),
                    operation_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retry_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    transport_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => new { x.message_type, x.transport_address });
                });

            migrationBuilder.CreateTable(
                name: "unacknowledged_retry_operations",
                columns: table => new
                {
                    request_id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    retry_type = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    originator = table.Column<string>(type: "text", nullable: true),
                    classifier = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    failed = table.Column<bool>(type: "boolean", nullable: false),
                    number_of_messages_processed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unacknowledged_retry_operations", x => new { x.request_id, x.retry_type });
                });

            migrationBuilder.CreateTable(
                name: "failed_message_groups",
                columns: table => new
                {
                    failed_message_unique_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_message_groups", x => new { x.failed_message_unique_id, x.group_id });
                    table.ForeignKey(
                        name: "fk_failed_message_groups_failed_messages_failed_message_unique",
                        column: x => x.failed_message_unique_id,
                        principalTable: "failed_messages",
                        principalColumn: "unique_message_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "licensing_endpoint_throughput",
                columns: table => new
                {
                    normalized_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    throughput_source = table.Column<int>(type: "integer", nullable: false),
                    date_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    message_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_licensing_endpoint_throughput", x => new { x.normalized_name, x.throughput_source, x.date_utc });
                    table.ForeignKey(
                        name: "fk_licensing_endpoint_throughput_licensing_endpoints_normalize",
                        columns: x => new { x.normalized_name, x.throughput_source },
                        principalTable: "licensing_endpoints",
                        principalColumns: new[] { "normalized_name", "throughput_source" },
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "ix_event_log_items_raised_at_id",
                table: "event_log_items",
                columns: new[] { "raised_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_failed_error_imports_failed_at",
                table: "failed_error_imports",
                column: "failed_at");

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_edits_edit_id",
                table: "failed_message_edits",
                column: "edit_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_group_id",
                table: "failed_message_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_type_group_id",
                table: "failed_message_groups",
                columns: new[] { "type", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_retries_retry_batch_id",
                table: "failed_message_retries",
                column: "retry_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_conversation_id",
                table: "failed_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_failing_endpoint_address",
                table: "failed_messages",
                column: "failing_endpoint_address");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_receiving_endpoint_name",
                table: "failed_messages",
                column: "receiving_endpoint_name");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_changed_at",
                table: "failed_messages",
                column: "status_changed_at",
                filter: "status IN (2, 4)");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_status_last_modified",
                table: "failed_messages",
                columns: new[] { "status", "last_modified" });

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_time_sent",
                table: "failed_messages",
                column: "time_sent");

            migrationBuilder.CreateIndex(
                name: "ix_historic_retry_operations_completion_time_id",
                table: "historic_retry_operations",
                columns: new[] { "completion_time", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_licensing_endpoint_throughput_date_utc",
                table: "licensing_endpoint_throughput",
                column: "date_utc");

            migrationBuilder.CreateIndex(
                name: "ix_licensing_endpoints_normalized_sanitized_name",
                table: "licensing_endpoints",
                column: "normalized_sanitized_name");

            migrationBuilder.CreateIndex(
                name: "ix_retry_batches_status_retry_session_id",
                table: "retry_batches",
                columns: new[] { "status", "retry_session_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archive_operations");

            migrationBuilder.DropTable(
                name: "custom_checks");

            migrationBuilder.DropTable(
                name: "endpoint_settings");

            migrationBuilder.DropTable(
                name: "event_log_items");

            migrationBuilder.DropTable(
                name: "external_integration_dispatch_requests");

            migrationBuilder.DropTable(
                name: "failed_error_imports");

            migrationBuilder.DropTable(
                name: "failed_message_edits");

            migrationBuilder.DropTable(
                name: "failed_message_groups");

            migrationBuilder.DropTable(
                name: "failed_message_retries");

            migrationBuilder.DropTable(
                name: "group_comments");

            migrationBuilder.DropTable(
                name: "historic_retry_operations");

            migrationBuilder.DropTable(
                name: "known_endpoints");

            migrationBuilder.DropTable(
                name: "licensing_endpoint_throughput");

            migrationBuilder.DropTable(
                name: "message_redirects");

            migrationBuilder.DropTable(
                name: "retry_batch_now_forwarding");

            migrationBuilder.DropTable(
                name: "retry_batches");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "unacknowledged_retry_operations");

            migrationBuilder.DropTable(
                name: "failed_messages");

            migrationBuilder.DropTable(
                name: "licensing_endpoints");
        }
    }
}
