using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddLicensingThroughput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "licensing_endpoints",
                columns: table => new
                {
                    normalized_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    throughput_source = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sanitized_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    normalized_sanitized_name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    user_indicator = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    scope = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    endpoint_indicators = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_licensing_endpoints", x => new { x.normalized_name, x.throughput_source });
                });

            migrationBuilder.CreateTable(
                name: "licensing_endpoint_throughput",
                columns: table => new
                {
                    normalized_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    throughput_source = table.Column<int>(type: "integer", nullable: false),
                    date_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    message_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_licensing_endpoint_throughput", x => new { x.normalized_name, x.throughput_source, x.date_utc });
                    table.ForeignKey(
                        name: "fk_licensing_endpoint_throughput_licensing_endpoints_normalize",
                        columns: x => new { x.normalized_name, x.throughput_source },
                        principalTable: "licensing_endpoints",
                        principalColumns: new[] { "normalized_name", "throughput_source" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_licensing_endpoint_throughput_date_utc",
                table: "licensing_endpoint_throughput",
                column: "date_utc");

            migrationBuilder.CreateIndex(
                name: "ix_licensing_endpoints_normalized_sanitized_name",
                table: "licensing_endpoints",
                column: "normalized_sanitized_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "licensing_endpoint_throughput");

            migrationBuilder.DropTable(
                name: "licensing_endpoints");
        }
    }
}
