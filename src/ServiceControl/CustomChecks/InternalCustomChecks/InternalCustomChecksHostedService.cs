namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus.CustomChecks;
    using NServiceBus.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;

    class InternalCustomChecksHostedService(
        IList<ICustomCheck> customChecks,
        HostInformation hostInfo,
        IAsyncTimer scheduler,
        CustomCheckResultProcessor checkResultProcessor,
        IOptions<PrimaryOptions> options,
        ILogger<InternalCustomChecksHostedService> logger)
        : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var check in customChecks)
            {
                var checkManager = new InternalCustomCheckManager(check, localEndpointDetails, scheduler, checkResultProcessor, logger);
                checkManager.Start();

                managers.Add(checkManager);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (managers.Any())
            {
                await Task.WhenAll(managers.Select(m => m.Stop()).ToArray());
            }
        }

        readonly EndpointDetails localEndpointDetails = new()
        {
            Host = hostInfo.DisplayName,
            HostId = hostInfo.HostId,
            Name = options.Value.InstanceName
        };
        IList<InternalCustomCheckManager> managers = [];
    }
}