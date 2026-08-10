using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddLicensingThroughput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LicensingEndpoints",
                columns: table => new
                {
                    NormalizedName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ThroughputSource = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SanitizedName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NormalizedSanitizedName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserIndicator = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EndpointIndicators = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingEndpoints", x => new { x.NormalizedName, x.ThroughputSource });
                });

            migrationBuilder.CreateTable(
                name: "LicensingEndpointThroughput",
                columns: table => new
                {
                    NormalizedName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ThroughputSource = table.Column<int>(type: "int", nullable: false),
                    DateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    MessageCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicensingEndpointThroughput", x => new { x.NormalizedName, x.ThroughputSource, x.DateUtc });
                    table.ForeignKey(
                        name: "FK_LicensingEndpointThroughput_LicensingEndpoints_NormalizedName_ThroughputSource",
                        columns: x => new { x.NormalizedName, x.ThroughputSource },
                        principalTable: "LicensingEndpoints",
                        principalColumns: new[] { "NormalizedName", "ThroughputSource" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicensingEndpoints_NormalizedSanitizedName",
                table: "LicensingEndpoints",
                column: "NormalizedSanitizedName");

            migrationBuilder.CreateIndex(
                name: "IX_LicensingEndpointThroughput_DateUtc",
                table: "LicensingEndpointThroughput",
                column: "DateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicensingEndpointThroughput");

            migrationBuilder.DropTable(
                name: "LicensingEndpoints");
        }
    }
}
