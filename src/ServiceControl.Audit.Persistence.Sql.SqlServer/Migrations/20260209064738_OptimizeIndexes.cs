using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "SagaSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_ConversationId_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_IsSystemMessage_TimeSent_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_MessageId",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_ReceivingEndpointName_IsSystemMessage_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_ReceivingEndpointName_IsSystemMessage_TimeSent_ProcessedAt",
                table: "ProcessedMessages");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_BatchId",
                table: "SagaSnapshots",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_BatchId",
                table: "ProcessedMessages",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_TimeSent",
                table: "ProcessedMessages",
                column: "TimeSent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaSnapshots_BatchId",
                table: "SagaSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_BatchId",
                table: "ProcessedMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_TimeSent",
                table: "ProcessedMessages");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "SagaSnapshots",
                columns: new[] { "BatchId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "ProcessedMessages",
                columns: new[] { "BatchId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ConversationId_ProcessedAt",
                table: "ProcessedMessages",
                columns: new[] { "ConversationId", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_IsSystemMessage_TimeSent_ProcessedAt",
                table: "ProcessedMessages",
                columns: new[] { "IsSystemMessage", "TimeSent", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_MessageId",
                table: "ProcessedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ReceivingEndpointName_IsSystemMessage_ProcessedAt",
                table: "ProcessedMessages",
                columns: new[] { "ReceivingEndpointName", "IsSystemMessage", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_ReceivingEndpointName_IsSystemMessage_TimeSent_ProcessedAt",
                table: "ProcessedMessages",
                columns: new[] { "ReceivingEndpointName", "IsSystemMessage", "TimeSent", "ProcessedAt" });
        }
    }
}
