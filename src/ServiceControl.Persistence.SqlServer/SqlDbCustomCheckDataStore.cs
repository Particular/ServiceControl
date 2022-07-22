namespace ServiceControl.Persistence.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;
    using ServiceControl.CustomChecks;
    using ServiceControl.Persistence;

    class SqlDbCustomCheckDataStore : ICustomChecksDataStore
    {
        public SqlDbCustomCheckDataStore(SqlDbConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        public async Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail)
        {
            var status = CheckStateChange.Unchanged;
            var id = detail.GetDeterministicId();

            await connectionManager.Perform(async connection =>
            {
                var customCheck = await connection.QueryFirstOrDefaultAsync(
                    "SELECT * FROM [CustomChecks] WHERE [Id] = @Id",
                    new { Id = id }).ConfigureAwait(false);

                if (customCheck == null ||
                    ((int)customCheck.Status == (int)Status.Fail && !detail.HasFailed) ||
                    ((int)customCheck.Status == (int)Status.Pass && detail.HasFailed))
                {
                    status = CheckStateChange.Changed;
                }

                await connection.ExecuteAsync(
                    @"IF EXISTS(SELECT * FROM [CustomChecks] WHERE Id = @Id)
                            UPDATE [CustomChecks] SET
                                [CustomCheckId] = @CustomCheckId,
                                [Category] = @Category,
                                [Status] = @Status,
                                [ReportedAt] = @ReportedAt,
                                [FailureReason] = @FailureReason,
                                [OriginatingEndpointName] = @OriginatingEndpointName,
                                [OriginatingEndpointHost] = @OriginatingEndpointHost,
                                [OriginatingEndpointHostId] = @OriginatingEndpointHostId
                            WHERE [Id] = @Id
                          ELSE
                            INSERT INTO [CustomChecks](Id, CustomCheckId, Category, Status, ReportedAt, FailureReason, OriginatingEndpointName, OriginatingEndpointHost, OriginatingEndpointHostId) 
                            VALUES(@Id, @CustomCheckId, @Category, @Status, @ReportedAt, @FailureReason, @OriginatingEndpointName, @OriginatingEndpointHost, @OriginatingEndpointHostId)",
                    new
                    {
                        Id = id,
                        detail.CustomCheckId,
                        detail.Category,
                        detail.ReportedAt,
                        detail.FailureReason,
                        Status = detail.HasFailed ? Status.Fail : Status.Pass,
                        OriginatingEndpointName = detail.OriginatingEndpoint.Name,
                        OriginatingEndpointHostId = detail.OriginatingEndpoint.HostId,
                        OriginatingEndpointHost = detail.OriginatingEndpoint.Host
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return status;
        }

        public async Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null)
        {
            var checks = new List<CustomCheck>();
            var totalCount = 0;
            _ = Enum.TryParse<Status>(status, true, out var checkStatus);

            return await connectionManager.PagedQuery(async connection =>
            {
                var filter = @" WHERE [Status] = @Status ";
                var query = @"SELECT * FROM [CustomChecks] " + filter +
                            @"ORDER BY ID
                              OFFSET @Offset ROWS 
                              FETCH NEXT  @Next   ROWS ONLY;";

                var countQuery = @"SELECT COUNT(Id) FROM [CustomChecks] " + filter;

                using (var multi = await connection.QueryMultipleAsync(query + countQuery, new { paging.Offset, paging.Next, Status = checkStatus }).ConfigureAwait(false))
                {
                    var rows = await multi.ReadAsync().ConfigureAwait(false);
                    foreach (dynamic row in rows)
                    {
                        checks.Add(new CustomCheck
                        {
                            Id = row.Id,
                            CustomCheckId = row.CustomCheckId,
                            Status = (Status)row.Status,
                            Category = row.Category,
                            FailureReason = row.FailureReason,
                            ReportedAt = row.ReportedAt,
                            OriginatingEndpoint = new EndpointDetails
                            {
                                Name = row.OriginatingEndpointName,
                                HostId = row.OriginatingEndpointHostId,
                                Host = row.OriginatingEndpointHost
                            }
                        });
                    }
                    totalCount = multi.ReadFirst<int>();
                }

                return new QueryResult<IList<CustomCheck>>(checks, new QueryStatsInfo(null, totalCount));
            }).ConfigureAwait(false);
        }

        readonly SqlDbConnectionManager connectionManager;
    }
}