namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;

    class LicenseLicenseMetadataProvider(IRavenSessionProvider sessionProvider) : ILicenseLicenseMetadataProvider
    {
        public async Task<TrialMetadata> GetLicenseMetadata(CancellationToken cancellationToken)
        {
            using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
            {
                return await session.LoadAsync<TrialMetadata>(TrialMetadata.TrialMetadataId, cancellationToken);
            }
        }

        public async Task InsertLicenseMetadata(TrialMetadata licenseMetadata, CancellationToken cancellationToken)
        {
            using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
            {
                await session.StoreAsync(licenseMetadata, TrialMetadata.TrialMetadataId, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }
        }
    }
}