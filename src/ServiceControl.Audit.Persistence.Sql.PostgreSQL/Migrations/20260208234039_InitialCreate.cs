using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "failed_audit_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_json = table.Column<string>(type: "text", nullable: false),
                    exception_info = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failed_audit_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "known_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "text", nullable: false),
                    last_seen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_endpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "known_endpoints_insert_only",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    known_endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "text", nullable: false),
                    last_seen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_endpoints_insert_only", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unique_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    searchable_content = table.Column<string>(type: "text", nullable: true),
                    message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    message_type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    time_sent = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_system_message = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    conversation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    receiving_endpoint_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    critical_time_ticks = table.Column<long>(type: "bigint", nullable: true),
                    processing_time_ticks = table.Column<long>(type: "bigint", nullable: true),
                    delivery_time_ticks = table.Column<long>(type: "bigint", nullable: true),
                    body_size = table.Column<int>(type: "integer", nullable: false),
                    body_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body_not_stored = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saga_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    saga_id = table.Column<Guid>(type: "uuid", nullable: false),
                    saga_type = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finish_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    state_after_change = table.Column<string>(type: "text", nullable: false),
                    initiating_message_json = table.Column<string>(type: "text", nullable: false),
                    outgoing_messages_json = table.Column<string>(type: "text", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saga_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_known_endpoints_last_seen",
                table: "known_endpoints",
                column: "last_seen");

            migrationBuilder.CreateIndex(
                name: "IX_known_endpoints_insert_only_known_endpoint_id",
                table: "known_endpoints_insert_only",
                column: "known_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "IX_known_endpoints_insert_only_last_seen",
                table: "known_endpoints_insert_only",
                column: "last_seen");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_conversation_id_processed_at",
                table: "processed_messages",
                columns: new[] { "conversation_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_is_system_message_time_sent_processed_at",
                table: "processed_messages",
                columns: new[] { "is_system_message", "time_sent", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_message_id",
                table: "processed_messages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_processed_at",
                table: "processed_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_receiving_endpoint_name_is_system_messa~1",
                table: "processed_messages",
                columns: new[] { "receiving_endpoint_name", "is_system_message", "time_sent", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_receiving_endpoint_name_is_system_messag~",
                table: "processed_messages",
                columns: new[] { "receiving_endpoint_name", "is_system_message", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_unique_message_id",
                table: "processed_messages",
                column: "unique_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "processed_messages",
                columns: new[] { "batch_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_saga_snapshots_processed_at",
                table: "saga_snapshots",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_saga_snapshots_saga_id",
                table: "saga_snapshots",
                column: "saga_id");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "saga_snapshots",
                columns: new[] { "batch_id", "processed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "failed_audit_imports");

            migrationBuilder.DropTable(
                name: "known_endpoints");

            migrationBuilder.DropTable(
                name: "known_endpoints_insert_only");

            migrationBuilder.DropTable(
                name: "processed_messages");

            migrationBuilder.DropTable(
                name: "saga_snapshots");
        }
    }
}
