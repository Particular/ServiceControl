using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "saga_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_conversation_id_processed_at",
                table: "processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_is_system_message_time_sent_processed_at",
                table: "processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_message_id",
                table: "processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_receiving_endpoint_name_is_system_messa~1",
                table: "processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_receiving_endpoint_name_is_system_messag~",
                table: "processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "processed_messages");

            migrationBuilder.CreateIndex(
                name: "IX_saga_snapshots_batch_id",
                table: "saga_snapshots",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_batch_id",
                table: "processed_messages",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_time_sent",
                table: "processed_messages",
                column: "time_sent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_saga_snapshots_batch_id",
                table: "saga_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_batch_id",
                table: "processed_messages");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_time_sent",
                table: "processed_messages");

            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_BatchId_ProcessedAt",
                table: "saga_snapshots",
                columns: new[] { "batch_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_conversation_id_processed_at",
                table: "processed_messages",
                columns: new[] { "conversation_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_is_system_message_time_sent_processed_at",
                table: "processed_messages",
                columns: new[] { "is_system_message", "time_sent", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_message_id",
                table: "processed_messages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_receiving_endpoint_name_is_system_messa~1",
                table: "processed_messages",
                columns: new[] { "receiving_endpoint_name", "is_system_message", "time_sent", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_receiving_endpoint_name_is_system_messag~",
                table: "processed_messages",
                columns: new[] { "receiving_endpoint_name", "is_system_message", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_BatchId_ProcessedAt",
                table: "processed_messages",
                columns: new[] { "batch_id", "processed_at" });
        }
    }
}
