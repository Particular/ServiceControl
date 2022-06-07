namespace ServiceControl.CustomChecks.Internal
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Hosting;

    class InternalCustomChecksHostedService : IHostedService
    {
        public InternalCustomChecksHostedService(IList<ICustomCheck> customChecks, ICustomChecksBackend store, HostInformation hostInfo, IAsyncTimer scheduler, string endpointName)
        {
            this.customChecks = customChecks;
            this.store = store;
            this.scheduler = scheduler;
            localEndpointDetails = new EndpointDetails
            {
                Host = hostInfo.DisplayName,
                HostId = hostInfo.HostId,
                Name = endpointName
            };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var check in customChecks)
            {
                var checkManager = new InternalCustomCheckManager(store, check, localEndpointDetails, scheduler);
                checkManager.Start();

                managers.Add(checkManager);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (managers.Any())
            {
                await Task.WhenAll(managers.Select(m => m.Stop()).ToArray())
                    .ConfigureAwait(false);
            }
        }

        IList<ICustomCheck> customChecks;
        readonly ICustomChecksBackend store;
        readonly IAsyncTimer scheduler;
        readonly EndpointDetails localEndpointDetails;
        IList<InternalCustomCheckManager> managers = new List<InternalCustomCheckManager>();
    }
}