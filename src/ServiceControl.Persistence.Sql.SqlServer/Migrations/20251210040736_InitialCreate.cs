#nullable disable

namespace ServiceControl.Persistence.Sql.SqlServer.Migrations
{
    using System;
    using Microsoft.EntityFrameworkCore.Migrations;

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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThroughputSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MessageCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyThroughput", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicensingMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThroughputEndpoint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EndpointName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ThroughputSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SanitizedEndpointName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndpointIndicators = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserIndicator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastCollectedData = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThroughputEndpoint", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrialLicense",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    TrialEndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialLicense", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UC_DailyThroughput_EndpointName_ThroughputSource_Date",
                table: "DailyThroughput",
                columns: new[] { "EndpointName", "ThroughputSource", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicensingMetadata_Key",
                table: "LicensingMetadata",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UC_ThroughputEndpoint_EndpointName_ThroughputSource",
                table: "ThroughputEndpoint",
                columns: new[] { "EndpointName", "ThroughputSource" },
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
