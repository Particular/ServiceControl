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
            // SQL Server requires a FULLTEXT catalog before creating FULLTEXT indexes
            // Must run outside transaction as CREATE FULLTEXT CATALOG cannot be in a transaction
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ProcessedMessagesCatalog')
                BEGIN
                    CREATE FULLTEXT CATALOG ProcessedMessagesCatalog AS DEFAULT;
                END
            ", suppressTransaction: true);

            // Create FULLTEXT index on HeadersJson and Body columns
            // This enables fast full-text search across both columns
            migrationBuilder.Sql(@"
                CREATE FULLTEXT INDEX ON ProcessedMessages(HeadersJson, Body)
                    KEY INDEX PK_ProcessedMessages
                    WITH STOPLIST = SYSTEM;
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('ProcessedMessages'))
                BEGIN
                    DROP FULLTEXT INDEX ON ProcessedMessages;
                END
            ", suppressTransaction: true);
        }
    }
}
