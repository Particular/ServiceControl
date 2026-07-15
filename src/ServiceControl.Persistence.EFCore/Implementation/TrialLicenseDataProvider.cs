namespace ServiceControl.Persistence.EFCore.Implementation;

public class TrialLicenseDataProvider : ITrialLicenseDataProvider
{
    public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
