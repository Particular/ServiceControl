namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILicenseLicenseMetadataProvider
    {
        Task<TrialMetadata> GetLicenseMetadata(CancellationToken cancellationToken);
        Task InsertLicenseMetadata(TrialMetadata licenseMetadata, CancellationToken cancellationToken);
    }
}
