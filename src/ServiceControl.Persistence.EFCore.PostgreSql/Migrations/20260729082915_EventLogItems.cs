using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class EventLogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventLogItems",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    unique_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    raised_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    related_to = table.Column<List<string>>(type: "text[]", nullable: false),
                    category = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    event_type = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_log_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_log_items_raised_at_id",
                table: "EventLogItems",
                columns: new[] { "raised_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_event_log_items_unique_event_id",
                table: "EventLogItems",
                column: "unique_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventLogItems");
        }
    }
}
