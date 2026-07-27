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
        => ExecuteWithDbContext(context =>
            context.TrialMetadata
                .Where(t => t.Id == TrialMetadataEntity.TrialMetadataId)
                .ExecuteUpdateAsync(e => e.SetProperty(p => p.TrialEndDate, trialEndDate), cancellationToken: cancellationToken)
        );
}