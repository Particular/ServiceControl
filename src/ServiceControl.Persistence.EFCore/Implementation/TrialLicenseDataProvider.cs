namespace ServiceControl.Persistence.EFCore.Implementation;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class TrialLicenseDataProvider(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), ITrialLicenseDataProvider
{
    public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken)
        => ExecuteWithDbContext(async context =>
        {
            var trialMetadata = await context.TrialMetadata.SingleAsync(t => t.Id == TrialMetadataEntity.TrialMetadataId, cancellationToken);
            return trialMetadata.TrialEndDate;
        });

    public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken)
        => ExecuteWithDbContext(async context =>
        {
            var trialMetadata = await context.TrialMetadata.SingleAsync(t => t.Id == TrialMetadataEntity.TrialMetadataId, cancellationToken);
            trialMetadata.TrialEndDate = trialEndDate;
            await (Task)context.SaveChangesAsync(cancellationToken);
        });
}