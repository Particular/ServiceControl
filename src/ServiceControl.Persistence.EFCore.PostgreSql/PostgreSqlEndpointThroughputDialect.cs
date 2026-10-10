namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class PostgreSqlEndpointThroughputDialect : PostgreSqlDialect, IEndpointThroughputDialect
{
    public async Task RecordEndpointThroughput(ServiceControlDbContext dbContext, string normalizedName, ThroughputSource throughputSource, IReadOnlyList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default)
    {
        foreach (var (date, messageCount) in throughput)
        {
            // INSERT ... ON CONFLICT is the atomic add-or-insert: the insert arm creates the day, the
            // conflict arm adds to the day's total. PostgreSQL serializes concurrent inserts of the
            // same key inside the uniqueness check, so there is no window in which a duplicate key
            // can surface to the application at all.
            var table = Table<LicensingEndpointThroughputEntity>(dbContext);
            await Execute(
                dbContext,
                $"""
                 INSERT INTO {table} (normalized_name, throughput_source, date_utc, message_count)
                 VALUES (@p0, @p1, @p2, @p3)
                 ON CONFLICT (normalized_name, throughput_source, date_utc)
                 DO UPDATE SET message_count = {table}.message_count + EXCLUDED.message_count
                 """,
                [[normalizedName, (int)throughputSource, date, messageCount]],
                cancellationToken);
        }
    }
}