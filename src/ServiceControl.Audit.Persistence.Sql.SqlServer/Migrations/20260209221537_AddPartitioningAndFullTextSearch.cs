using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitioningAndFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create partition function on ProcessedAt (daily boundaries)
            // Initial boundary is a sentinel — real partitions are created at runtime
            migrationBuilder.Sql("""
                CREATE PARTITION FUNCTION pf_ProcessedAt (datetime2)
                AS RANGE RIGHT FOR VALUES ();
                """);

            // Create partition scheme mapping all partitions to PRIMARY filegroup
            migrationBuilder.Sql("""
                CREATE PARTITION SCHEME ps_ProcessedAt
                AS PARTITION pf_ProcessedAt ALL TO ([PRIMARY]);
                """);

            // Recreate ProcessedMessages on the partition scheme
            // Drop existing table and indexes, then recreate on the partition scheme
            migrationBuilder.Sql("""
                -- Save any existing indexes/constraints info
                ALTER TABLE [ProcessedMessages] DROP CONSTRAINT [PK_ProcessedMessages];
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS [IX_ProcessedMessages_UniqueMessageId_ProcessedAt] ON [ProcessedMessages];
                DROP INDEX IF EXISTS [IX_ProcessedMessages_MessageId_ProcessedAt] ON [ProcessedMessages];
                DROP INDEX IF EXISTS [IX_ProcessedMessages_ConversationId_ProcessedAt] ON [ProcessedMessages];
                """);

            // Recreate ProcessedMessages table on partition scheme
            migrationBuilder.Sql("""
                DECLARE @sql NVARCHAR(MAX);
                SELECT @sql = 'ALTER TABLE [ProcessedMessages] ADD CONSTRAINT [PK_ProcessedMessages] PRIMARY KEY CLUSTERED ([Id], [ProcessedAt]) ON ps_ProcessedAt([ProcessedAt])';
                EXEC(@sql);
                """);

            // Recreate indexes as partition-aligned (on the partition scheme)
            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_UniqueMessageId_ProcessedAt]
                ON [ProcessedMessages] ([UniqueMessageId], [ProcessedAt])
                ON ps_ProcessedAt([ProcessedAt]);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_MessageId_ProcessedAt]
                ON [ProcessedMessages] ([MessageId], [ProcessedAt])
                ON ps_ProcessedAt([ProcessedAt]);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_ConversationId_ProcessedAt]
                ON [ProcessedMessages] ([ConversationId], [ProcessedAt])
                ON ps_ProcessedAt([ProcessedAt]);
                """);

            // Recreate SagaSnapshots on the partition scheme
            migrationBuilder.Sql("""
                ALTER TABLE [SagaSnapshots] DROP CONSTRAINT [PK_SagaSnapshots];
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS [IX_SagaSnapshots_SagaId_ProcessedAt] ON [SagaSnapshots];
                """);

            migrationBuilder.Sql("""
                DECLARE @sql NVARCHAR(MAX);
                SELECT @sql = 'ALTER TABLE [SagaSnapshots] ADD CONSTRAINT [PK_SagaSnapshots] PRIMARY KEY CLUSTERED ([Id], [ProcessedAt]) ON ps_ProcessedAt([ProcessedAt])';
                EXEC(@sql);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_SagaSnapshots_SagaId_ProcessedAt]
                ON [SagaSnapshots] ([SagaId], [ProcessedAt])
                ON ps_ProcessedAt([ProcessedAt]);
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE NONCLUSTERED INDEX [UX_ProcessedMessages_FullTextKey]
                ON [ProcessedMessages] ([Id])
                ON [PRIMARY];
                """);
            // Create FULLTEXT catalog and index for search
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ProcessedMessagesCatalog')
                BEGIN
                    CREATE FULLTEXT CATALOG ProcessedMessagesCatalog AS DEFAULT;
                END
                """, suppressTransaction: true);

            migrationBuilder.Sql("""
                CREATE FULLTEXT INDEX ON ProcessedMessages(SearchableContent LANGUAGE 0)
                    KEY INDEX UX_ProcessedMessages_FullTextKey
                    WITH STOPLIST = OFF;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop fulltext index
            migrationBuilder.Sql("""
                IF EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('ProcessedMessages'))
                BEGIN
                    DROP FULLTEXT INDEX ON ProcessedMessages;
                END
                """, suppressTransaction: true);

            // Move tables back to PRIMARY (non-partitioned)
            // Drop and recreate PKs/indexes without partition scheme
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS [IX_ProcessedMessages_UniqueMessageId_ProcessedAt] ON [ProcessedMessages];
                DROP INDEX IF EXISTS [IX_ProcessedMessages_MessageId_ProcessedAt] ON [ProcessedMessages];
                DROP INDEX IF EXISTS [IX_ProcessedMessages_ConversationId_ProcessedAt] ON [ProcessedMessages];
                ALTER TABLE [ProcessedMessages] DROP CONSTRAINT [PK_ProcessedMessages];
                ALTER TABLE [ProcessedMessages] ADD CONSTRAINT [PK_ProcessedMessages] PRIMARY KEY CLUSTERED ([Id], [ProcessedAt]);
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_UniqueMessageId_ProcessedAt] ON [ProcessedMessages] ([UniqueMessageId], [ProcessedAt]);
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_MessageId_ProcessedAt] ON [ProcessedMessages] ([MessageId], [ProcessedAt]);
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_ConversationId_ProcessedAt] ON [ProcessedMessages] ([ConversationId], [ProcessedAt]);
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS [IX_SagaSnapshots_SagaId_ProcessedAt] ON [SagaSnapshots];
                ALTER TABLE [SagaSnapshots] DROP CONSTRAINT [PK_SagaSnapshots];
                ALTER TABLE [SagaSnapshots] ADD CONSTRAINT [PK_SagaSnapshots] PRIMARY KEY CLUSTERED ([Id], [ProcessedAt]);
                CREATE NONCLUSTERED INDEX [IX_SagaSnapshots_SagaId_ProcessedAt] ON [SagaSnapshots] ([SagaId], [ProcessedAt]);
                """);

            // Drop partition scheme and function
            migrationBuilder.Sql("""
                DROP PARTITION SCHEME ps_ProcessedAt;
                DROP PARTITION FUNCTION pf_ProcessedAt;
                """);
        }
    }
}
