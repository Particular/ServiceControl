namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;

    class LicenseLicenseMetadataProvider(IRavenSessionProvider sessionProvider) : ILicenseLicenseMetadataProvider
    {
        public async Task<LicenseMetadata> GetLicenseMetadata(CancellationToken cancellationToken)
        {
            using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
            {
                return await session.LoadAsync<LicenseMetadata>(LicenseMetadata.LicenseMetadataId, cancellationToken);
            }
        }

        public async Task InsertLicenseMetadata(LicenseMetadata licenseMetadata, CancellationToken cancellationToken)
        {
            using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
            {
                await session.StoreAsync(licenseMetadata, LicenseMetadata.LicenseMetadataId, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }
        }
    }
}