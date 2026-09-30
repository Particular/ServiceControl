namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.CustomChecks;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class AuditPartitionCustomCheck(IServiceScopeFactory scopeFactory, IAuditPartitionManager partitions, TimeProvider timeProvider)
    : CustomCheck("Audit partition provisioning", "ServiceControl.Audit Health", TimeSpan.FromHours(1))
{
    public static readonly TimeSpan Threshold = TimeSpan.FromHours(12);

    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        var end = await partitions.ProvisionedUntil(dbContext, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (end is null)
        {
            return CheckResult.Failed("No audit partitions are provisioned, so audit ingestion cannot store anything. Run setup for this instance.");
        }

        if (end.Value - now < Threshold)
        {
            return CheckResult.Failed(
                $"Audit partitions are provisioned only until {end:u}, less than {Threshold.TotalHours:0} hours ahead. " +
                "The retention sweep provisions them and has not been able to. Audit ingestion stops once the last provisioned hour passes. " +
                "Check this instance's logs for the sweep failures, and run setup for this instance to provision partitions immediately.");
        }

        return CheckResult.Pass;
    }
}
