namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILicenseLicenseMetadataProvider
    {
        Task<LicenseMetadata> GetLicenseMetadata(CancellationToken cancellationToken);
        Task InsertLicenseMetadata(LicenseMetadata licenseMetadata, CancellationToken cancellationToken);
    }
}
