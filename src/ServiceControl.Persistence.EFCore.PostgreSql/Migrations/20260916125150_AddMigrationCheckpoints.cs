using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "migration_checkpoints",
                columns: table => new
                {
                    category_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    cursor = table.Column<string>(type: "text", nullable: true),
                    copied_count = table.Column<long>(type: "bigint", nullable: false),
                    skipped_count = table.Column<long>(type: "bigint", nullable: false),
                    source_total = table.Column<long>(type: "bigint", nullable: true),
                    skip_reasons = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_progress_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    settled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    already_present_count = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_migration_checkpoints", x => x.category_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "migration_checkpoints");
        }
    }
}
