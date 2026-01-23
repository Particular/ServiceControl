using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

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
                name: "FailedAuditImports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_json = table.Column<string>(type: "text", nullable: false),
                    exception_info = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedAuditImports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    unique_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
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
                    body_not_stored = table.Column<bool>(type: "boolean", nullable: false),
                    query = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(to_tsvector('english', coalesce(headers_json::text, '')), 'A') || setweight(to_tsvector('english', coalesce(body, '')), 'B')", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SagaSnapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                    table.PrimaryKey("PK_SagaSnapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_processed_messages_query",
                table: "ProcessedMessages",
                column: "query")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_conversation_id_processed_at",
                table: "ProcessedMessages",
                columns: new[] { "conversation_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_is_system_message_time_sent_processed_at",
                table: "ProcessedMessages",
                columns: new[] { "is_system_message", "time_sent", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_message_id",
                table: "ProcessedMessages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_processed_at",
                table: "ProcessedMessages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_receiving_endpoint_name_is_system_messag~1",
                table: "ProcessedMessages",
                columns: new[] { "receiving_endpoint_name", "is_system_message", "time_sent", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_receiving_endpoint_name_is_system_message~",
                table: "ProcessedMessages",
                columns: new[] { "receiving_endpoint_name", "is_system_message", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_unique_message_id",
                table: "ProcessedMessages",
                column: "unique_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_processed_at",
                table: "SagaSnapshots",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_saga_id",
                table: "SagaSnapshots",
                column: "saga_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedAuditImports");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");

            migrationBuilder.DropTable(
                name: "SagaSnapshots");
        }
    }
}
