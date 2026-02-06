using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextIndexForSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create FULLTEXT catalog if it doesn't exist
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ProcessedMessagesCatalog')
                BEGIN
                    CREATE FULLTEXT CATALOG ProcessedMessagesCatalog AS DEFAULT;
                END
                """, suppressTransaction: true);

            // Create FULLTEXT index on SearchableContent column
            // LANGUAGE 0 = neutral (no language-specific word breaking)
            // STOPLIST = OFF disables stop words for more precise matching
            migrationBuilder.Sql("""
                CREATE FULLTEXT INDEX ON ProcessedMessages(SearchableContent LANGUAGE 0)
                    KEY INDEX PK_ProcessedMessages
                    WITH STOPLIST = OFF;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('ProcessedMessages'))
                BEGIN
                    DROP FULLTEXT INDEX ON ProcessedMessages;
                END
                """, suppressTransaction: true);
        }
    }
}
