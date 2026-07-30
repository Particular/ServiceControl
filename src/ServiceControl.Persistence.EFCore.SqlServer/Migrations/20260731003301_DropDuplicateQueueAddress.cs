using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DropDuplicateQueueAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_QueueAddress",
                table: "FailedMessages");

            migrationBuilder.DropColumn(
                name: "QueueAddress",
                table: "FailedMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QueueAddress",
                table: "FailedMessages",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_QueueAddress",
                table: "FailedMessages",
                column: "QueueAddress");
        }
    }
}
