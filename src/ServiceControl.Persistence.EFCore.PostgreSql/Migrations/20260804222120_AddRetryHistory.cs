using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ix_historic_retry_operations_completion_time_id",
                table: "historic_retry_operations",
                columns: new[] { "completion_time", "id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historic_retry_operations");

            migrationBuilder.DropTable(
                name: "unacknowledged_retry_operations");
        }
    }
}
