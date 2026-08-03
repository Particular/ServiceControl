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
                    id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    group_name = table.Column<string>(type: "text", nullable: false),
                    archive_type = table.Column<int>(type: "integer", nullable: false),
                    is_archive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_archive_operations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archive_operations_request_id_archive_type_is_archive",
                table: "ArchiveOperations",
                columns: new[] { "request_id", "archive_type", "is_archive" },
                unique: true);

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
