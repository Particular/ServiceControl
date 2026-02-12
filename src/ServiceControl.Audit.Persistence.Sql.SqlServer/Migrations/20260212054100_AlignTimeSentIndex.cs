using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Audit.Persistence.Sql.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AlignTimeSentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align the TimeSent index with the partition scheme so it supports
            // partition elimination in queries.
            migrationBuilder.Sql("""
                DROP INDEX [IX_ProcessedMessages_TimeSent] ON [ProcessedMessages];

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_TimeSent]
                ON [ProcessedMessages] ([TimeSent], [CreatedOn])
                ON ps_CreatedOn([CreatedOn]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX [IX_ProcessedMessages_TimeSent] ON [ProcessedMessages];

                CREATE NONCLUSTERED INDEX [IX_ProcessedMessages_TimeSent]
                ON [ProcessedMessages] ([TimeSent])
                ON [PRIMARY];
                """);
        }
    }
}
