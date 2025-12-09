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
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TrialLicense");
        migrationBuilder.DropTable(
            name: "LicensingMetadata");
    }
}
