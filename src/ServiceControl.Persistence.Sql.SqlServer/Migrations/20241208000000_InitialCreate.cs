namespace ServiceControl.Persistence.Sql.SqlServer.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

/// <inheritdoc />
[DbContext(typeof(SqlServerDbContext))]
[Migration("20241208000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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

        migrationBuilder.CreateTable(
            name: "LicensingMetadata",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Key = table.Column<string>(type: "nvarchar(200)", nullable: false),
                Data = table.Column<string>(type: "nvarchar(2000)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LicensingMetadata", x => x.Id);
                table.UniqueConstraint("UC_LicensingMetadata_Key", t => t.Key);
            });

        migrationBuilder.CreateTable(
            name: "ThroughputEndpoint",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                EndpointName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                ThroughputSource = table.Column<string>(type: "nvarchar(50)", nullable: false),
                SanitizedEndpointName = table.Column<string>(type: "nvarchar(200)", nullable: true),
                EndpointIndicators = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                UserIndicator = table.Column<string>(type: "nvarchar(50)", nullable: true),
                Scope = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
                LastCollectedData = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ThrouhgputEndpoint", x => x.Id);
                table.UniqueConstraint("UC_ThroughputEndpoint_EndpointName_ThroughputSource", t => new
                {
                    t.EndpointName,
                    t.ThroughputSource
                });
            }
        );

        migrationBuilder.CreateTable(
            name: "DailyThroughput",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                EndpointName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                ThroughputSource = table.Column<string>(type: "nvarchar(50)", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                MessageCount = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DailyThroughput", x => x.Id);
                table.UniqueConstraint("UC_DailyThroughput_EndpointName_ThroughputSource_Date", e => new
                {
                    e.EndpointName,
                    e.ThroughputSource,
                    e.Date
                });
            }
        );

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TrialLicense");
        migrationBuilder.DropTable(
            name: "LicensingMetadata");
        migrationBuilder.DropTable(
            name: "ThroughputEndpoint");
        migrationBuilder.DropTable(
            name: "DailyThroughput");
    }
}