namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    class FakeConfigurationApi : IConfigurationApi
    {
        public object GetConfig() => throw new NotImplementedException();

        public Task<object> GetRemoteConfigs(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public RootUrls GetUrls(string baseUrl) => throw new NotImplementedException();
    }
}
