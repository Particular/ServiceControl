using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Full-text search index using MySQL's FULLTEXT index
            // This enables efficient MATCH...AGAINST searches on the Query column
            migrationBuilder.Sql(
                @"CREATE FULLTEXT INDEX IX_FailedMessages_Query_FTS
                  ON FailedMessages(Query)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop full-text search index
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS IX_FailedMessages_Query_FTS ON FailedMessages");
        }
    }
}
