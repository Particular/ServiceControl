using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailingEndpointAddress",
                table: "FailedMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EndpointSettings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TrackInstances = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointSettings", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "TrialMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrialEndDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialMetadata", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "TrialMetadata",
                columns: new[] { "Id", "TrialEndDate" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_FailingEndpointAddress",
                table: "FailedMessages",
                column: "FailingEndpointAddress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndpointSettings");

            migrationBuilder.DropTable(
                name: "TrialMetadata");

            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_FailingEndpointAddress",
                table: "FailedMessages");

            migrationBuilder.DropColumn(
                name: "FailingEndpointAddress",
                table: "FailedMessages");
        }
    }
}
