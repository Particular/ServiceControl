using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextIndexForSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create GIN index on tsvector expression for full-text search
            // 'simple' configuration = no stemming, no stop words, exact matching
            // Note: SearchableContent is always populated by the application (never null)
            // The expression must match exactly what EF.Functions.ToTsVector generates
            migrationBuilder.Sql("""
                CREATE INDEX ix_processed_messages_searchable_content
                ON processed_messages
                USING GIN (to_tsvector('simple', searchable_content));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_processed_messages_searchable_content;
                """);
        }
    }
}
