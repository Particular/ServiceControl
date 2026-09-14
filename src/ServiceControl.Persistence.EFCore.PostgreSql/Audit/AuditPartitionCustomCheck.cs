namespace ServiceControl.Persistence.EFCore.PostgreSql.Audit;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.CustomChecks;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

// Only the retention owner provisions partitions, so an ingestion worker cannot insert into an hour
// the owner never reached. This makes that condition visible before it bites, on every host.
class AuditPartitionCustomCheck(IServiceScopeFactory scopeFactory, IAuditPartitionManager partitions, TimeProvider timeProvider)
    : CustomCheck("Audit partition provisioning", "ServiceControl Health", TimeSpan.FromMinutes(5))
{
    public static readonly TimeSpan Threshold = TimeSpan.FromHours(12);

    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var end = await partitions.NewestProvisionedHourEnd(dbContext, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (end is null)
        {
            return CheckResult.Failed("No audit partitions are provisioned, so audit ingestion cannot store anything. Run setup on the instance that owns this database.");
        }

        if (end.Value - now < Threshold)
        {
            return CheckResult.Failed(
                $"Audit partitions are provisioned only until {end:u}, less than {Threshold.TotalHours:0} hours ahead. " +
                "The retention sweep provisions them, so the instance configured to run it is not sweeping. Audit ingestion stops once the last provisioned hour passes.");
        }

        return CheckResult.Pass;
    }
}
