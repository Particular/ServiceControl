using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UseSimpleFtsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing FULLTEXT index
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('ProcessedMessages'))
                BEGIN
                    DROP FULLTEXT INDEX ON ProcessedMessages;
                END
            ", suppressTransaction: true);

            // Recreate FULLTEXT index with STOPLIST = OFF and LANGUAGE 0 (neutral)
            // STOPLIST = OFF disables stop words filtering for more precise matches
            // LANGUAGE 0 uses neutral language settings
            migrationBuilder.Sql(@"
                CREATE FULLTEXT INDEX ON ProcessedMessages(HeadersJson LANGUAGE 0, Body LANGUAGE 0)
                    KEY INDEX PK_ProcessedMessages
                    WITH STOPLIST = OFF;
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the modified FULLTEXT index
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('ProcessedMessages'))
                BEGIN
                    DROP FULLTEXT INDEX ON ProcessedMessages;
                END
            ", suppressTransaction: true);

            // Recreate original FULLTEXT index with SYSTEM stoplist
            migrationBuilder.Sql(@"
                CREATE FULLTEXT INDEX ON ProcessedMessages(HeadersJson, Body)
                    KEY INDEX PK_ProcessedMessages
                    WITH STOPLIST = SYSTEM;
            ", suppressTransaction: true);
        }
    }
}
