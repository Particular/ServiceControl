namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class TrialLicenseDataProvider(IRavenSessionProvider sessionProvider) : ITrialLicenseDataProvider
    {
        public async Task<DateOnly?> GetTrialEndDate(CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            var document = await session.LoadAsync<TrialMetadata>(TrialMetadata.TrialMetadataId, cancellationToken);

            return document?.TrialEndDate;
        }

        public async Task StoreTrialEndDate(DateOnly trialEndDate, CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            await session.StoreAsync(new TrialMetadata { TrialEndDate = trialEndDate }, TrialMetadata.TrialMetadataId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}