namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.SqlServer;
    using ServiceControl.Persistence.Tests;

    class PersistenceTestsConfiguration : EFPersistenceTestsConfiguration
    {
        public override string Name => "SQLServer";

        public override Task<string> GetConnectionString() => SqlServerSharedContainer.GetConnectionStringAsync();

        protected override Task CreateSchema(string connectionString, string schema) => TestSchema.Create(connectionString, schema);

        protected override Task DropSchema(string connectionString, string schema) => TestSchema.Drop(connectionString, schema);

        public override EFPersistenceConfigurationBase CreateConfiguration() => new SqlServerPersistenceConfiguration();

        protected override IAuditMessagesViewDataStore DecorateMessagesViewStore(IAuditMessagesViewDataStore store) =>
            new SearchesWaitForFullTextIndex(store, WaitForFullTextIndex);

        public async Task WaitForFullTextIndex(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT CAST(OBJECTPROPERTYEX(OBJECT_ID(@table), 'TableFullTextPendingChanges') AS int)
                         + CAST(OBJECTPROPERTYEX(OBJECT_ID(@table), 'TableFullTextPopulateStatus') AS int)
                    """;
                command.Parameters.AddWithValue("@table", $"[{Schema}].[AuditMessages]");

                if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return;
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The full text index did not catch up with the audit messages table.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }

        sealed class SearchesWaitForFullTextIndex(IAuditMessagesViewDataStore store, Func<CancellationToken, Task> waitForFullTextIndex) : IAuditMessagesViewDataStore
        {
            public async Task<QueryResult<IList<MessagesView>>> QueryMessages(string searchParam, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default)
            {
                await waitForFullTextIndex(cancellationToken);
                return await store.QueryMessages(searchParam, pagingInfo, sortInfo, timeSentRange, cancellationToken);
            }

            public async Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpointAndKeyword(string endpoint, string keyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default)
            {
                await waitForFullTextIndex(cancellationToken);
                return await store.QueryMessagesByReceivingEndpointAndKeyword(endpoint, keyword, pagingInfo, sortInfo, timeSentRange, cancellationToken);
            }

            public Task<QueryResult<IList<MessagesView>>> GetMessages(bool includeSystemMessages, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default) =>
                store.GetMessages(includeSystemMessages, pagingInfo, sortInfo, timeSentRange, cancellationToken);

            public Task<QueryResult<IList<MessagesView>>> QueryMessagesByReceivingEndpoint(bool includeSystemMessages, string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange timeSentRange = null, CancellationToken cancellationToken = default) =>
                store.QueryMessagesByReceivingEndpoint(includeSystemMessages, endpointName, pagingInfo, sortInfo, timeSentRange, cancellationToken);

            public Task<QueryResult<IList<MessagesView>>> QueryMessagesByConversationId(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) =>
                store.QueryMessagesByConversationId(conversationId, pagingInfo, sortInfo, cancellationToken);

            public Task<MessageBodyView> GetMessageBody(string messageId, CancellationToken cancellationToken = default) =>
                store.GetMessageBody(messageId, cancellationToken);

            public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default) =>
                store.QueryAuditCounts(endpointName, cancellationToken);
        }
    }
}
