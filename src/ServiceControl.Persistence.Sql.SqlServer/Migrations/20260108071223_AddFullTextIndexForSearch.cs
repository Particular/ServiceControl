using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextIndexForSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server requires a FULLTEXT catalog before creating FULLTEXT indexes
            // Must run outside transaction as CREATE FULLTEXT CATALOG cannot be in a transaction
            // migrationBuilder.Sql(@"
            //     IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'FailedMessagesCatalog')
            //     BEGIN
            //         CREATE FULLTEXT CATALOG FailedMessagesCatalog AS DEFAULT;
            //     END
            // ", suppressTransaction: true);

            // Create FULLTEXT index on HeadersJson and Body columns
            // This enables fast full-text search across both columns
            // migrationBuilder.Sql(@"
            //     CREATE FULLTEXT INDEX ON FailedMessages(HeadersJson, Body)
            //         KEY INDEX PK_FailedMessages
            //         WITH STOPLIST = SYSTEM;
            // ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FULLTEXT index
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('FailedMessages'))
                BEGIN
                    DROP FULLTEXT INDEX ON FailedMessages;
                END
            ", suppressTransaction: true);

            // Note: We don't drop the FULLTEXT catalog as it might be used by other tables
        }
    }
}
