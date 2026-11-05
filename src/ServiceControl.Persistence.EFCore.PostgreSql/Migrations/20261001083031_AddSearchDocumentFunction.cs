using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchDocumentFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old inline expression index before installing the function that replaces it.
            migrationBuilder.Sql(FullTextSearchSql.DropIndex);

            // Install the search-document function before creating the GIN index that references it.
            migrationBuilder.Sql(FullTextSearchSql.CreateFunction);

            // Create the function-call GIN index (same index name, now backed by the function).
            migrationBuilder.Sql(FullTextSearchSql.CreateFunctionIndex);

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_message_id",
                table: "failed_messages",
                column: "message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the function-call GIN index before dropping the function it depends on.
            migrationBuilder.Sql(FullTextSearchSql.DropIndex);

            // Drop the function now that no index references it.
            migrationBuilder.Sql(FullTextSearchSql.DropFunction);

            migrationBuilder.DropIndex(
                name: "ix_failed_messages_message_id",
                table: "failed_messages");

            // Recreate the preceding inline expression index.
            migrationBuilder.Sql(FullTextSearchSql.CreateIndex);
        }
    }
}