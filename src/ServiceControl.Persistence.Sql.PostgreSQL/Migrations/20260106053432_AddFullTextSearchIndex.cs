using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Full-text search index using PostgreSQL's GIN index on tsvector expression
            // This enables efficient full-text search on the Query column using to_tsvector and websearch_to_tsquery
            migrationBuilder.Sql(
                @"CREATE INDEX IX_FailedMessages_query_fts
                  ON ""FailedMessages""
                  USING GIN (to_tsvector('english', COALESCE(query, '')))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop full-text search index
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_FailedMessages_query_fts");
        }
    }
}
