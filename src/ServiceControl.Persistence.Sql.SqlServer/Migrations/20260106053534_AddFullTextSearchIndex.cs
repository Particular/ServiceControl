using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Full-text search catalog and index using SQL Server Full-Text Search
            // This enables efficient FREETEXT searches on the Query column
            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ServiceControlCatalog')
                  BEGIN
                      CREATE FULLTEXT CATALOG ServiceControlCatalog AS DEFAULT;
                  END");

            migrationBuilder.Sql(
                @"CREATE FULLTEXT INDEX ON FailedMessages(Query LANGUAGE 1033)
                  KEY INDEX PK_FailedMessages
                  ON ServiceControlCatalog
                  WITH CHANGE_TRACKING AUTO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop full-text search index and catalog
            migrationBuilder.Sql(
                @"IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('FailedMessages'))
                  BEGIN
                      DROP FULLTEXT INDEX ON FailedMessages;
                  END");

            migrationBuilder.Sql(
                @"IF EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ServiceControlCatalog')
                  BEGIN
                      DROP FULLTEXT CATALOG ServiceControlCatalog;
                  END");
        }
    }
}
