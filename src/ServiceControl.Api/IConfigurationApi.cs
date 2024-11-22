namespace ServiceControl.Api
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;

    public interface IConfigurationApi
    {
        public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken);
        public Task<object> GetConfig(CancellationToken cancellationToken);
        public Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken);
    }
}