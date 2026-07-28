using System;
using Microsoft.EntityFrameworkCore.Migrations;

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
                name: "failed_message_retries",
                columns: table => new
                {
                    unique_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retry_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
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
                    queue_address = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
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
                    body_content_type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_failed_messages", x => x.unique_message_id);
                });

            migrationBuilder.CreateTable(
                name: "known_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host = table.Column<string>(type: "text", nullable: false),
                    monitored = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_known_endpoints", x => x.id);
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

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_groups_group_id",
                table: "failed_message_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_conversation_id",
                table: "failed_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_queue_address",
                table: "failed_messages",
                column: "queue_address");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "failed_message_groups");

            migrationBuilder.DropTable(
                name: "failed_message_retries");

            migrationBuilder.DropTable(
                name: "known_endpoints");

            migrationBuilder.DropTable(
                name: "failed_messages");
        }
    }
}
