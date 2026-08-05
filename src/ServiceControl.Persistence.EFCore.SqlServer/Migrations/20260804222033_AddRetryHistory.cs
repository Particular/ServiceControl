using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricRetryOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Originator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Failed = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfMessagesProcessed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricRetryOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnacknowledgedRetryOperations",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Originator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Classifier = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Failed = table.Column<bool>(type: "bit", nullable: false),
                    NumberOfMessagesProcessed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnacknowledgedRetryOperations", x => new { x.RequestId, x.RetryType });
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricRetryOperations_CompletionTime_Id",
                table: "HistoricRetryOperations",
                columns: new[] { "CompletionTime", "Id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricRetryOperations");

            migrationBuilder.DropTable(
                name: "UnacknowledgedRetryOperations");
        }
    }
}
