namespace ServiceControl.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;

    public interface IConfigurationApi
    {
        Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken);

        Task<object> GetConfig(CancellationToken cancellationToken);

        Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken);
    }
}
