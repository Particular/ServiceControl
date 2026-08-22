using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_messages",
                columns: table => new
                {
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    message_type = table.Column<string>(type: "text", nullable: true),
                    time_sent = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    conversation_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    is_system_message = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    sending_endpoint_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    sending_endpoint_host_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sending_endpoint_host = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    receiving_endpoint_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    receiving_endpoint_host_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receiving_endpoint_host = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    critical_time_ticks = table.Column<long>(type: "bigint", nullable: true),
                    processing_time_ticks = table.Column<long>(type: "bigint", nullable: true),
                    delivery_time_ticks = table.Column<long>(type: "bigint", nullable: true),
                    headers_json = table.Column<string>(type: "text", nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: true),
                    body_stored_externally = table.Column<bool>(type: "boolean", nullable: false),
                    body_size = table.Column<int>(type: "integer", nullable: false),
                    body_content_type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_messages", x => new { x.created_on, x.id });
                });

            migrationBuilder.CreateTable(
                name: "failed_audit_imports",
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
                    table.PrimaryKey("pk_failed_audit_imports", x => x.unique_message_id);
                });

            migrationBuilder.CreateTable(
                name: "saga_snapshots",
                columns: table => new
                {
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    saga_id = table.Column<Guid>(type: "uuid", nullable: false),
                    saga_type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finish_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    state_after_change = table.Column<string>(type: "text", nullable: true),
                    initiating_message_json = table.Column<string>(type: "text", nullable: true),
                    outgoing_messages_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saga_snapshots", x => new { x.created_on, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_messages_conversation_id",
                table: "audit_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_messages_processed_at",
                table: "audit_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_messages_receiving_endpoint_name_created_on",
                table: "audit_messages",
                columns: new[] { "receiving_endpoint_name", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_messages_time_sent",
                table: "audit_messages",
                column: "time_sent");

            migrationBuilder.CreateIndex(
                name: "ix_audit_messages_unique_message_id",
                table: "audit_messages",
                column: "unique_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_audit_imports_failed_at",
                table: "failed_audit_imports",
                column: "failed_at");

            migrationBuilder.CreateIndex(
                name: "ix_saga_snapshots_saga_id_finish_time",
                table: "saga_snapshots",
                columns: new[] { "saga_id", "finish_time" });

            // Last, so that the clone picks up the columns, primary keys and indexes above.
            migrationBuilder.Sql(AuditPartitioningSql.PartitionTables());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AuditPartitioningSql.UnpartitionTables());

            migrationBuilder.DropTable(
                name: "audit_messages");

            migrationBuilder.DropTable(
                name: "failed_audit_imports");

            migrationBuilder.DropTable(
                name: "saga_snapshots");
        }
    }
}
