using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create partition function and scheme for hourly partitions on CreatedOn.
            // Actual partition boundaries are created at runtime by the partition manager.
            migrationBuilder.Sql("""
                CREATE PARTITION FUNCTION pf_CreatedOn (datetime2)
                AS RANGE RIGHT FOR VALUES ();
                """);

            migrationBuilder.Sql("""
                CREATE PARTITION SCHEME ps_CreatedOn
                AS PARTITION pf_CreatedOn ALL TO ([PRIMARY]);
                """);

            // Move ProcessedMessages onto the partition scheme
            migrationBuilder.Sql("""
                ALTER TABLE [ProcessedMessages] DROP CONSTRAINT [PK_ProcessedMessages];
                """);

            migrationBuilder.Sql("""
                DROP INDEX [IX_ProcessedMessages_UniqueMessageId_CreatedOn] ON [ProcessedMessages];
                DROP INDEX [IX_ProcessedMessages_MessageId_CreatedOn] ON [ProcessedMessages];
                DROP INDEX [IX_ProcessedMessages_ConversationId_CreatedOn] ON [ProcessedMessages];
                DROP INDEX [IX_ProcessedMessages_TimeSent] ON [ProcessedMessages];
                """);

            migrationBuilder.Sql("""
                DECLARE @sql NVARCHAR(MAX);
                SELECT @sql = 'ALTER TABLE [ProcessedMessages] ADD CONSTRAINT [PK_ProcessedMessages] PRIMARY KEY CLUSTERED ([Id], [CreatedOn]) ON ps_CreatedOn([CreatedOn])';
                EXEC(@sql);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_UniqueMessageId_CreatedOn]
                ON [ProcessedMessages] ([UniqueMessageId], [CreatedOn])
                ON ps_CreatedOn([CreatedOn]);

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_MessageId_CreatedOn]
                ON [ProcessedMessages] ([MessageId], [CreatedOn])
                ON ps_CreatedOn([CreatedOn]);

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_ConversationId_CreatedOn]
                ON [ProcessedMessages] ([ConversationId], [CreatedOn])
                ON ps_CreatedOn([CreatedOn]);

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_TimeSent]
                ON [ProcessedMessages] ([TimeSent], [CreatedOn])
                ON ps_CreatedOn([CreatedOn]);
                """);

            // Move SagaSnapshots onto the partition scheme
            migrationBuilder.Sql("""
                ALTER TABLE [SagaSnapshots] DROP CONSTRAINT [PK_SagaSnapshots];
                """);

            migrationBuilder.Sql("""
                DROP INDEX [IX_SagaSnapshots_SagaId_CreatedOn] ON [SagaSnapshots];
                """);

            migrationBuilder.Sql("""
                DECLARE @sql NVARCHAR(MAX);
                SELECT @sql = 'ALTER TABLE [SagaSnapshots] ADD CONSTRAINT [PK_SagaSnapshots] PRIMARY KEY CLUSTERED ([Id], [CreatedOn]) ON ps_CreatedOn([CreatedOn])';
                EXEC(@sql);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_SagaSnapshots_SagaId_CreatedOn]
                ON [SagaSnapshots] ([SagaId], [CreatedOn])
                ON ps_CreatedOn([CreatedOn]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Move ProcessedMessages back to PRIMARY
            migrationBuilder.Sql("""
                ALTER TABLE [ProcessedMessages] DROP CONSTRAINT [PK_ProcessedMessages];
                """);

            migrationBuilder.Sql("""
                DROP INDEX [IX_ProcessedMessages_UniqueMessageId_CreatedOn] ON [ProcessedMessages];
                DROP INDEX [IX_ProcessedMessages_MessageId_CreatedOn] ON [ProcessedMessages];
                DROP INDEX [IX_ProcessedMessages_ConversationId_CreatedOn] ON [ProcessedMessages];
                DROP INDEX [IX_ProcessedMessages_TimeSent] ON [ProcessedMessages];
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [ProcessedMessages] ADD CONSTRAINT [PK_ProcessedMessages] PRIMARY KEY CLUSTERED ([Id], [CreatedOn]);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_UniqueMessageId_CreatedOn]
                ON [ProcessedMessages] ([UniqueMessageId], [CreatedOn]);

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_MessageId_CreatedOn]
                ON [ProcessedMessages] ([MessageId], [CreatedOn]);

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_ConversationId_CreatedOn]
                ON [ProcessedMessages] ([ConversationId], [CreatedOn]);

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_TimeSent]
                ON [ProcessedMessages] ([TimeSent]);
                """);

            // Move SagaSnapshots back to PRIMARY
            migrationBuilder.Sql("""
                ALTER TABLE [SagaSnapshots] DROP CONSTRAINT [PK_SagaSnapshots];
                """);

            migrationBuilder.Sql("""
                DROP INDEX [IX_SagaSnapshots_SagaId_CreatedOn] ON [SagaSnapshots];
                """);

            migrationBuilder.Sql("""
                ALTER TABLE [SagaSnapshots] ADD CONSTRAINT [PK_SagaSnapshots] PRIMARY KEY CLUSTERED ([Id], [CreatedOn]);
                """);

            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX [IX_SagaSnapshots_SagaId_CreatedOn]
                ON [SagaSnapshots] ([SagaId], [CreatedOn]);
                """);

            migrationBuilder.Sql("""
                DROP PARTITION SCHEME ps_CreatedOn;
                DROP PARTITION FUNCTION pf_CreatedOn;
                """);
        }
    }
}
