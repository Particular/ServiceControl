namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.Extensions;

    class SqlDbCustomCheckDataStore : ICustomChecksStorage
    {
        public SqlDbCustomCheckDataStore(string connectionString, IDomainEvents domainEvents)
        {
            this.connectionString = connectionString;
            this.domainEvents = domainEvents;
        }

        public async Task UpdateCustomCheckStatus(CustomCheckDetail detail)
        {
            var publish = false;
            var id = DeterministicGuid.MakeId(detail.OriginatingEndpoint.Name, detail.OriginatingEndpoint.HostId.ToString(), detail.CustomCheckId);

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var customCheck = await connection.QueryFirstOrDefaultAsync(
                    "SELECT * FROM [CustomChecks] WHERE [Id] = @Id",
                    new { Id = id }).ConfigureAwait(false);

                if (customCheck == null ||
                    ((int)customCheck.Status == (int)Status.Fail && !detail.HasFailed) ||
                    ((int)customCheck.Status == (int)Status.Pass && detail.HasFailed))
                {
                    publish = true;
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
            }

            if (publish)
            {
                if (detail.HasFailed)
                {
                    await domainEvents.Raise(new CustomCheckFailed
                    {
                        Id = id,
                        CustomCheckId = detail.CustomCheckId,
                        Category = detail.Category,
                        FailedAt = detail.ReportedAt,
                        FailureReason = detail.FailureReason,
                        OriginatingEndpoint = detail.OriginatingEndpoint
                    }).ConfigureAwait(false);
                }
                else
                {
                    await domainEvents.Raise(new CustomCheckSucceeded
                    {
                        Id = id,
                        CustomCheckId = detail.CustomCheckId,
                        Category = detail.Category,
                        SucceededAt = detail.ReportedAt,
                        OriginatingEndpoint = detail.OriginatingEndpoint
                    }).ConfigureAwait(false);
                }
            }
        }

        public async Task<StatisticsResult> GetStats(HttpRequestMessage request, string status = null)
        {
            var results = new StatisticsResult
            {
                Checks = new List<CustomCheck>()
            };

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var filter = status != null ? " WHERE [Status] = @Status" : "";
                var query = @"SELECT * FROM [CustomChecks] " + filter +
                            @"ORDER BY ID
                              OFFSET @Offset ROWS 
                              FETCH NEXT  @Next   ROWS ONLY;";

                var countQuery = @"SELECT COUNT(Id) FROM [CustomChecks] " + filter;

                var paging = GetPaging(request);
                using (var multi = await connection.QueryMultipleAsync(query + countQuery, paging).ConfigureAwait(false))
                {
                    var rows = await multi.ReadAsync().ConfigureAwait(false);
                    foreach (dynamic row in rows)
                    {
                        results.Checks.Add(new CustomCheck
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
                    results.TotalResults = multi.ReadFirst<int>();
                }
            }

            return results;
        }

        public static async Task Setup(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

                var createCommand = $@"
                    IF NOT EXISTS (
                         SELECT *
                         FROM {catalog}.sys.objects
                         WHERE object_id = OBJECT_ID(N'CustomChecks') AND type in (N'U')
                       )
                       BEGIN
                           CREATE TABLE [dbo].[CustomChecks](
                               [Id] [uniqueidentifier] NOT NULL,
                               [CustomCheckId] nvarchar(300) NOT NULL,
                               [Category] nvarchar(300) NULL,
                               [Status] int NOT NULL,
                               [ReportedAt] datetime NOT NULL,
                               [FailureReason] nvarchar(300) NULL,
                               [OriginatingEndpointName] nvarchar(300) NOT NULL,
                               [OriginatingEndpointHostId] [uniqueidentifier] NOT NULL,
                               [OriginatingEndpointHost] nvarchar(300) NOT NULL
                           ) ON [PRIMARY]
                       END";

                connection.Open();

                await connection.ExecuteAsync(createCommand).ConfigureAwait(false);
            }
        }

        static PagingInfo GetPaging(HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);
            if (page < 1)
            {
                page = 1;
            }

            return new PagingInfo(page, maxResultsPerPage);
        }

        readonly string connectionString;
        readonly IDomainEvents domainEvents;

        class PagingInfo
        {
            public int Page { get; }
            public int PageSize { get; }
            public int Offset { get; }
            public int Next { get; }

            public PagingInfo(int page, int pageSize)
            {
                Page = page;
                PageSize = pageSize;
                Next = pageSize;
                Offset = (Page - 1) * Next;
            }
        }
    }
}