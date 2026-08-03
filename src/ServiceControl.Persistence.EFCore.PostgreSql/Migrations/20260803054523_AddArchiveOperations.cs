using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveOperations",
                columns: table => new
                {
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    archive_type = table.Column<int>(type: "integer", nullable: false),
                    is_archive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_archive_operations", x => new { x.request_id, x.archive_type, x.is_archive });
                });

            migrationBuilder.CreateIndex(
                name: "ix_archive_operations_started",
                table: "ArchiveOperations",
                column: "started");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveOperations");
        }
    }
}
