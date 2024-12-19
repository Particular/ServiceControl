namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITrialLicenseDataProvider
    {
        Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken);
        Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken);
    }
}