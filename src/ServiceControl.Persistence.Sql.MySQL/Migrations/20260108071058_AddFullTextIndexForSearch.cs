using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextIndexForSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create FULLTEXT index on HeadersJson and Body columns for full-text search
            // MySQL supports FULLTEXT indexes on longtext columns
            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_HeadersJson_Body",
                table: "FailedMessages",
                columns: new[] { "HeadersJson", "Body" })
                .Annotation("MySql:FullTextIndex", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedMessages_HeadersJson_Body",
                table: "FailedMessages");
        }
    }
}
