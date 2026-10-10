namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// The provider-specific SQL of recording days of endpoint throughput. Recording adds to a day's
/// total and creates the day's row when absent, and it must do so as one atomic statement: the same
/// endpoint/day is recorded concurrently (monitoring throughput messages are processed with high
/// concurrency, and more than one collector can report the same source), so a check-then-insert in
/// application code races and any insert conflict it leaves behind surfaces as a duplicate-key
/// failure to the collectors. Implementations run on the DbContext connection inside the
/// transaction the caller has already opened, like the ingestion dialects.
/// </summary>
public interface IEndpointThroughputDialect
{
    /// <summary>
    /// One atomic add-or-insert per day, in the order given (the caller orders by date so concurrent
    /// calls covering overlapping days take row locks in the same order and cannot deadlock).
    /// </summary>
    Task RecordEndpointThroughput(ServiceControlDbContext dbContext, string normalizedName, ThroughputSource throughputSource, IReadOnlyList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default);
}