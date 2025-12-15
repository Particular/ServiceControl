#nullable disable

namespace ServiceControl.Persistence.Sql.PostgreSQL.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyThroughput",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    endpoint_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    throughput_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    message_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_throughput", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LicensingMetadata",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_licensing_metadata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ThroughputEndpoint",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    endpoint_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    throughput_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sanitized_endpoint_name = table.Column<string>(type: "text", nullable: true),
                    endpoint_indicators = table.Column<string>(type: "text", nullable: true),
                    user_indicator = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: true),
                    last_collected_data = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_endpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "TrialLicense",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    trial_end_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_trial_licenses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "UC_DailyThroughput_EndpointName_ThroughputSource_Date",
                table: "DailyThroughput",
                columns: new[] { "endpoint_name", "throughput_source", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicensingMetadata_key",
                table: "LicensingMetadata",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UC_ThroughputEndpoint_EndpointName_ThroughputSource",
                table: "ThroughputEndpoint",
                columns: new[] { "endpoint_name", "throughput_source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyThroughput");

            migrationBuilder.DropTable(
                name: "LicensingMetadata");

            migrationBuilder.DropTable(
                name: "ThroughputEndpoint");

            migrationBuilder.DropTable(
                name: "TrialLicense");
        }
    }
}
