using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCompositeRetentionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_saga_snapshots_batch_id",
                table: "saga_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_batch_id",
                table: "processed_messages");

            migrationBuilder.CreateIndex(
                name: "IX_saga_snapshots_batch_id_processed_at",
                table: "saga_snapshots",
                columns: new[] { "batch_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_batch_id_processed_at",
                table: "processed_messages",
                columns: new[] { "batch_id", "processed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_saga_snapshots_batch_id_processed_at",
                table: "saga_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_processed_messages_batch_id_processed_at",
                table: "processed_messages");

            migrationBuilder.CreateIndex(
                name: "IX_saga_snapshots_batch_id",
                table: "saga_snapshots",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_processed_messages_batch_id",
                table: "processed_messages",
                column: "batch_id");
        }
    }
}
