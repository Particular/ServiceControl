using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveOperations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    group_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    archive_type = table.Column<int>(type: "integer", nullable: false),
                    archive_state = table.Column<int>(type: "integer", nullable: false),
                    total_number_of_messages = table.Column<int>(type: "integer", nullable: false),
                    number_of_messages_archived = table.Column<int>(type: "integer", nullable: false),
                    number_of_batches = table.Column<int>(type: "integer", nullable: false),
                    current_batch = table.Column<int>(type: "integer", nullable: false),
                    started = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completion_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_archive_operations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CustomChecks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_check_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    endpoint_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_custom_checks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "DailyThroughput",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    endpoint_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    throughput_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    message_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_throughput", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "EndpointSettings",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    track_instances = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointSettings", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "EventLogItems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    raised_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    related_to_json = table.Column<string>(type: "jsonb", maxLength: 4000, nullable: true),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_log_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIntegrationDispatchRequests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dispatch_context_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_external_integration_dispatch_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FailedErrorImports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_json = table.Column<string>(type: "jsonb", nullable: false),
                    exception_info = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_failed_error_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessageRetries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    failed_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retry_batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stage_attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_failed_message_retries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unique_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    processing_attempts_json = table.Column<string>(type: "jsonb", nullable: false),
                    failure_groups_json = table.Column<string>(type: "jsonb", nullable: false),
                    headers_json = table.Column<string>(type: "jsonb", nullable: false),
                    primary_failure_group_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    message_type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    time_sent = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sending_endpoint_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receiving_endpoint_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    exception_type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    exception_message = table.Column<string>(type: "text", nullable: true),
                    queue_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    number_of_processing_attempts = table.Column<int>(type: "integer", nullable: true),
                    last_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    conversation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_failed_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "GroupComments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_group_comments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "KnownEndpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    host_display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monitored = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_known_endpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LicensingMetadata",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_licensing_metadata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MessageBodies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body_size = table.Column<int>(type: "integer", nullable: false),
                    etag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_message_bodies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MessageRedirects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    e_tag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    redirects_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_message_redirects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationsSettings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_settings_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_notifications_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "QueueAddresses",
                columns: table => new
                {
                    physical_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    failed_message_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueAddresses", x => x.physical_address);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    context = table.Column<string>(type: "text", nullable: true),
                    retry_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    staging_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    originator = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    classifier = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    request_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    initial_batch_size = table.Column<int>(type: "integer", nullable: false),
                    retry_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    failure_retries_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_retry_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatchNowForwarding",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    retry_batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_retry_batch_now_forwarding", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "RetryHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    historic_operations_json = table.Column<string>(type: "jsonb", nullable: true),
                    unacknowledged_operations_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_retry_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message_type_type_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    message_type_version = table.Column<int>(type: "integer", nullable: false),
                    subscribers_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ThroughputEndpoint",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    endpoint_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    throughput_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sanitized_endpoint_name = table.Column<string>(type: "text", nullable: true),
                    endpoint_indicators = table.Column<string>(type: "text", nullable: true),
                    user_indicator = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: true),
                    last_collected_data = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_endpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "TrialLicense",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    trial_end_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_trial_licenses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_archive_state",
                table: "ArchiveOperations",
                column: "archive_state");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_archive_type_request_id",
                table: "ArchiveOperations",
                columns: new[] { "archive_type", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_request_id",
                table: "ArchiveOperations",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_CustomChecks_status",
                table: "CustomChecks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "UC_DailyThroughput_EndpointName_ThroughputSource_Date",
                table: "DailyThroughput",
                columns: new[] { "endpoint_name", "throughput_source", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventLogItems_raised_at",
                table: "EventLogItems",
                column: "raised_at");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIntegrationDispatchRequests_created_at",
                table: "ExternalIntegrationDispatchRequests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageRetries_failed_message_id",
                table: "FailedMessageRetries",
                column: "failed_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageRetries_retry_batch_id",
                table: "FailedMessageRetries",
                column: "retry_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_conversation_id_last_processed_at",
                table: "FailedMessages",
                columns: new[] { "conversation_id", "last_processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_message_id",
                table: "FailedMessages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_message_type_time_sent",
                table: "FailedMessages",
                columns: new[] { "message_type", "time_sent" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_primary_failure_group_id_status_last_process~",
                table: "FailedMessages",
                columns: new[] { "primary_failure_group_id", "status", "last_processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_queue_address_status_last_processed_at",
                table: "FailedMessages",
                columns: new[] { "queue_address", "status", "last_processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_receiving_endpoint_name_status_last_processe~",
                table: "FailedMessages",
                columns: new[] { "receiving_endpoint_name", "status", "last_processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_receiving_endpoint_name_time_sent",
                table: "FailedMessages",
                columns: new[] { "receiving_endpoint_name", "time_sent" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_status_last_processed_at",
                table: "FailedMessages",
                columns: new[] { "status", "last_processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_status_queue_address",
                table: "FailedMessages",
                columns: new[] { "status", "queue_address" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_unique_message_id",
                table: "FailedMessages",
                column: "unique_message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupComments_group_id",
                table: "GroupComments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_LicensingMetadata_key",
                table: "LicensingMetadata",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_retry_session_id",
                table: "RetryBatches",
                column: "retry_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_staging_id",
                table: "RetryBatches",
                column: "staging_id");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_status",
                table: "RetryBatches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatchNowForwarding_retry_batch_id",
                table: "RetryBatchNowForwarding",
                column: "retry_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_message_type_type_name_message_type_version",
                table: "Subscriptions",
                columns: new[] { "message_type_type_name", "message_type_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UC_ThroughputEndpoint_EndpointName_ThroughputSource",
                table: "ThroughputEndpoint",
                columns: new[] { "endpoint_name", "throughput_source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveOperations");

            migrationBuilder.DropTable(
                name: "CustomChecks");

            migrationBuilder.DropTable(
                name: "DailyThroughput");

            migrationBuilder.DropTable(
                name: "EndpointSettings");

            migrationBuilder.DropTable(
                name: "EventLogItems");

            migrationBuilder.DropTable(
                name: "ExternalIntegrationDispatchRequests");

            migrationBuilder.DropTable(
                name: "FailedErrorImports");

            migrationBuilder.DropTable(
                name: "FailedMessageRetries");

            migrationBuilder.DropTable(
                name: "FailedMessages");

            migrationBuilder.DropTable(
                name: "GroupComments");

            migrationBuilder.DropTable(
                name: "KnownEndpoints");

            migrationBuilder.DropTable(
                name: "LicensingMetadata");

            migrationBuilder.DropTable(
                name: "MessageBodies");

            migrationBuilder.DropTable(
                name: "MessageRedirects");

            migrationBuilder.DropTable(
                name: "NotificationsSettings");

            migrationBuilder.DropTable(
                name: "QueueAddresses");

            migrationBuilder.DropTable(
                name: "RetryBatches");

            migrationBuilder.DropTable(
                name: "RetryBatchNowForwarding");

            migrationBuilder.DropTable(
                name: "RetryHistory");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "ThroughputEndpoint");

            migrationBuilder.DropTable(
                name: "TrialLicense");
        }
    }
}
