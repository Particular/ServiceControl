namespace ServiceControl.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITrialLicenseMetadataProvider
    {
        Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken);
        Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken);
    }
}
