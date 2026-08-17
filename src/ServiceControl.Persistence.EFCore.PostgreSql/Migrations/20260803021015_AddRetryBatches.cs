using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "retry_id",
                table: "failed_message_retries");

            migrationBuilder.AddColumn<Guid>(
                name: "retry_batch_id",
                table: "failed_message_retries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "stage_attempts",
                table: "failed_message_retries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.CreateIndex(
                name: "ix_failed_message_retries_retry_batch_id",
                table: "failed_message_retries",
                column: "retry_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_retry_batches_status_retry_session_id",
                table: "retry_batches",
                columns: new[] { "status", "retry_session_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retry_batch_now_forwarding");

            migrationBuilder.DropTable(
                name: "retry_batches");

            migrationBuilder.DropIndex(
                name: "ix_failed_message_retries_retry_batch_id",
                table: "failed_message_retries");

            migrationBuilder.DropColumn(
                name: "retry_batch_id",
                table: "failed_message_retries");

            migrationBuilder.DropColumn(
                name: "stage_attempts",
                table: "failed_message_retries");

            migrationBuilder.AddColumn<string>(
                name: "retry_id",
                table: "failed_message_retries",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);
        }
    }
}
