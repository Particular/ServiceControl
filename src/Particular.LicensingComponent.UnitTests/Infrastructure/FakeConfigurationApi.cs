namespace Particular.LicensingComponent.UnitTests.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    class FakeConfigurationApi : IConfigurationApi
    {
        public Task<object> GetConfig(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}