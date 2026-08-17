namespace ServiceControl.Connection
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IProvidePlatformConnectionDetails
    {
        Task ProvideConnectionDetails(PlatformConnectionDetails connection, CancellationToken cancellationToken = default);
    }
}