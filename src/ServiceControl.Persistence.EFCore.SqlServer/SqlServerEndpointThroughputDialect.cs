namespace ServiceControl.Persistence.EFCore.SqlServer;

using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SqlServerEndpointThroughputDialect : SqlServerDialect, IEndpointThroughputDialect
{
    public async Task RecordEndpointThroughput(ServiceControlDbContext dbContext, string normalizedName, ThroughputSource throughputSource, IReadOnlyList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default)
    {
        foreach (var (date, messageCount) in throughput)
        {
            // MERGE is the atomic add-or-insert: the WHEN MATCHED arm adds to the day's total, the
            // WHEN NOT MATCHED arm creates the day. HOLDLOCK serializes concurrent recorders on the
            // same day's key range, so there is no window in which two inserts of the same day can
            // both pass the match test.
            await Execute(
                dbContext,
                $"""
                 MERGE {Table<LicensingEndpointThroughputEntity>(dbContext)} WITH (HOLDLOCK) AS t
                 USING (VALUES (@p0, @p1, @p2, @p3)) AS s (NormalizedEndpointName, Source, Day, Count)
                 ON t.NormalizedName = s.NormalizedEndpointName AND t.ThroughputSource = s.Source AND t.DateUtc = s.Day
                 WHEN MATCHED THEN UPDATE SET t.MessageCount = t.MessageCount + s.Count
                 WHEN NOT MATCHED THEN INSERT (NormalizedName, ThroughputSource, DateUtc, MessageCount)
                 VALUES (s.NormalizedEndpointName, s.Source, s.Day, s.Count);
                 """,
                [[normalizedName, (int)throughputSource, date, messageCount]],
                cancellationToken);
        }
    }
}