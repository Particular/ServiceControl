namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class TrialLicenseDataProvider(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), ITrialLicenseDataProvider
{
    public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken)
        => ExecuteWithDbContext(context =>
            context.GetSetting<DateOnly?>(SettingKeys.TrialEndDate, cancellationToken));

    public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken)
        => ExecuteWithDbContext(context =>
            context.StoreSetting(SettingKeys.TrialEndDate, trialEndDate, cancellationToken));
}
