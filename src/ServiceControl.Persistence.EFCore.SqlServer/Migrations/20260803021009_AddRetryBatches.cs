using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetryId",
                table: "FailedMessageRetries");

            migrationBuilder.AddColumn<Guid>(
                name: "RetryBatchId",
                table: "FailedMessageRetries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "StageAttempts",
                table: "FailedMessageRetries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RetryBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetrySessionId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RetryType = table.Column<int>(type: "int", nullable: false),
                    InitialBatchSize = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Last = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StagingId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Context = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Originator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Classifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    InitiatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetryBatchNowForwarding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    RetryBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryBatchNowForwarding", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageRetries_RetryBatchId",
                table: "FailedMessageRetries",
                column: "RetryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RetryBatches_Status_RetrySessionId",
                table: "RetryBatches",
                columns: new[] { "Status", "RetrySessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetryBatches");

            migrationBuilder.DropTable(
                name: "RetryBatchNowForwarding");

            migrationBuilder.DropIndex(
                name: "IX_FailedMessageRetries_RetryBatchId",
                table: "FailedMessageRetries");

            migrationBuilder.DropColumn(
                name: "RetryBatchId",
                table: "FailedMessageRetries");

            migrationBuilder.DropColumn(
                name: "StageAttempts",
                table: "FailedMessageRetries");

            migrationBuilder.AddColumn<string>(
                name: "RetryId",
                table: "FailedMessageRetries",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }
    }
}
