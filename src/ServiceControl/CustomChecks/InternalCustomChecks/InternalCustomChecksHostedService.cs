namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Infrastructure.BackgroundTasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.CustomChecks;
    using NServiceBus.Hosting;

    class InternalCustomChecksHostedService : IHostedService
    {
        public InternalCustomChecksHostedService(
            IList<ICustomCheck> customChecks,
            ICustomChecksDataStore store,
            IDomainEvents domainEvents,
            HostInformation hostInfo,
            IAsyncTimer scheduler,
            string endpointName)
        {
            this.customChecks = customChecks;
            this.store = store;
            this.scheduler = scheduler;
            this.domainEvents = domainEvents;
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
                var checkManager = new InternalCustomCheckManager(store, check, localEndpointDetails, scheduler, domainEvents);
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
        readonly ICustomChecksDataStore store;
        readonly IAsyncTimer scheduler;
        readonly IDomainEvents domainEvents;
        readonly EndpointDetails localEndpointDetails;
        IList<InternalCustomCheckManager> managers = new List<InternalCustomCheckManager>();
    }
}