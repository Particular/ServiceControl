using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCompositeRetentionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaSnapshots_BatchId",
                table: "SagaSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_BatchId",
                table: "ProcessedMessages");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "SagaSnapshots",
                columns: new[] { "BatchId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "ProcessedMessages",
                columns: new[] { "BatchId", "ProcessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "SagaSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_BatchId",
                table: "SagaSnapshots",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_BatchId",
                table: "ProcessedMessages",
                column: "BatchId");
        }
    }
}
