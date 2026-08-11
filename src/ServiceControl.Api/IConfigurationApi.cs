namespace ServiceControl.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;

    public interface IConfigurationApi
    {
        Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken = default);

        Task<object> GetConfig(CancellationToken cancellationToken = default);

        Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken = default);
    }
}
