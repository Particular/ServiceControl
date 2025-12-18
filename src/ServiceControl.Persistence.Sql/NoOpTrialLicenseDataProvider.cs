namespace ServiceControl.Persistence.Sql;

using System;
using System.Threading;
using System.Threading.Tasks;

class NoOpTrialLicenseDataProvider : ITrialLicenseDataProvider
{
    static readonly DateOnly FutureDate = new DateOnly(2099, 12, 31);

    public Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken) =>
        Task.FromResult<DateOnly?>(FutureDate);

    public Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
