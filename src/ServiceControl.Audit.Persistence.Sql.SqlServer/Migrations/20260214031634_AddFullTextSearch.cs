using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE NONCLUSTERED INDEX [UX_ProcessedMessages_FullTextKey]
                ON [ProcessedMessages] ([Id])
                ON [PRIMARY];
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ProcessedMessagesCatalog')
                BEGIN
                    CREATE FULLTEXT CATALOG ProcessedMessagesCatalog AS DEFAULT;
                END
                """, suppressTransaction: true);

            migrationBuilder.Sql("""
                CREATE FULLTEXT INDEX ON ProcessedMessages(SearchableContent LANGUAGE 0)
                    KEY INDEX UX_ProcessedMessages_FullTextKey
                    ON ProcessedMessagesCatalog
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

            migrationBuilder.Sql("""
                IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('ProcessedMessages') AND name = 'UX_ProcessedMessages_FullTextKey')
                BEGIN
                    DROP INDEX [UX_ProcessedMessages_FullTextKey] ON [ProcessedMessages];
                END
                """);
        }
    }
}
