namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Dapper;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.Extensions;
    using Lucene.Net.Analysis.Tokenattributes;
    using Raven.Database.Storage.Voron.Impl;

    class SqlDbCustomCheckDataStore : ICustomChecksStorage
    {
        public SqlDbCustomCheckDataStore(string connectionString, IDomainEvents domainEvents, EndpointDetailsMapper mapper)
        {
            this.connectionString = connectionString;
            this.domainEvents = domainEvents;
            this.mapper = mapper;
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
                                [OriginatingEndpoint] = @OriginatingEndpoint
                            WHERE [Id] = @Id
                          ELSE
                            INSERT INTO [CustomChecks](Id, CustomCheckId, Category, Status, ReportedAt, FailureReason, OriginatingEndpoint) 
                            VALUES(@Id, @CustomCheckId, @Category, @Status, @ReportedAt, @FailureReason, @OriginatingEndpoint)",
                    new
                    {
                        Id = id,
                        detail.CustomCheckId,
                        detail.Category,
                        detail.ReportedAt,
                        detail.FailureReason,
                        Status = detail.HasFailed ? Status.Fail : Status.Pass,
                        OriginatingEndpoint = mapper.Serialize(detail.OriginatingEndpoint)
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
            var results = new StatisticsResult();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                var query = @"SELECT * FROM [CustomChecks]
                              OFFSET      @Offset ROWS 
                              FETCH NEXT  @Next   ROWS ONLY";

                var countQuery = @"SELECT COUNT(Id) FROM [CustomChecks]";

                if (status != null)
                {
                    query += " WHERE [Status] = @Status";
                    countQuery += " WHERE [Status] = @Status";
                }

                var paging = GetPaging(request);
                using (var multi = await connection.QueryMultipleAsync(query + countQuery, paging).ConfigureAwait(false))
                {
                    results.TotalResults = multi.ReadFirst<int>();
                    var rows = await multi.ReadAsync().ConfigureAwait(false);
                    foreach (dynamic row in rows)
                    {
                        results.Checks.Add(new CustomCheck
                        {
                            Id = row.Id,
                            CustomCheckId = row.CustomCheckId,
                            Status = row.Status,
                            Category = row.Category,
                            FailureReason = row.FailureReason,
                            ReportedAt = row.ReportedAt,
                            OriginatingEndpoint = mapper.Parse(row.OriginatingEndpoint)
                        });
                    }
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
                               [OriginatingEndpoint] nvarchar(300) NOT NULL,
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
        readonly EndpointDetailsMapper mapper;

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