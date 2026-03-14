using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedOnIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SagaSnapshots_CreatedOn",
                table: "SagaSnapshots",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessages_CreatedOn",
                table: "ProcessedMessages",
                column: "CreatedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SagaSnapshots_CreatedOn",
                table: "SagaSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedMessages_CreatedOn",
                table: "ProcessedMessages");
        }
    }
}
