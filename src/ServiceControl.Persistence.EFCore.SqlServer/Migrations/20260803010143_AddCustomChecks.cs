using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomCheckId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatingEndpointName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginatingEndpointHostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginatingEndpointHost = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomChecks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomChecks_ReportedAt",
                table: "CustomChecks",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomChecks_Status_ReportedAt",
                table: "CustomChecks",
                columns: new[] { "Status", "ReportedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomChecks");
        }
    }
}
