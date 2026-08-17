namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.EFCore.Infrastructure;

public class TrialLicenseDataProvider(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), ITrialLicenseDataProvider
{
    public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken = default)
        => ExecuteWithDbContext((context, token) =>
            context.GetSetting<DateOnly?>(SettingKeys.TrialEndDate, token), cancellationToken);

    public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken = default)
        => ExecuteWithDbContext((context, token) =>
            context.StoreSetting(SettingKeys.TrialEndDate, trialEndDate, token), cancellationToken);
}
