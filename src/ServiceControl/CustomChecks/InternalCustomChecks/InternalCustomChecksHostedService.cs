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
            HostInformation hostInfo,
            IAsyncTimer scheduler,
            CustomCheckResultProcessor checkResultProcessor,
            string endpointName)
        {
            this.customChecks = customChecks;
            this.scheduler = scheduler;
            this.checkResultProcessor = checkResultProcessor;
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
                var checkManager = new InternalCustomCheckManager(check, localEndpointDetails, scheduler, checkResultProcessor);
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
        readonly IAsyncTimer scheduler;
        readonly CustomCheckResultProcessor checkResultProcessor;
        readonly EndpointDetails localEndpointDetails;
        IList<InternalCustomCheckManager> managers = new List<InternalCustomCheckManager>();
    }
}