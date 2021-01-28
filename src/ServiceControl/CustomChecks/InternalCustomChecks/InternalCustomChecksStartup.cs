namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.CustomChecks;
    using NServiceBus.Features;
    using NServiceBus.Hosting;

    class InternalCustomChecksStartup : FeatureStartupTask
    {
        public InternalCustomChecksStartup(IList<ICustomCheck> customChecks, CustomChecksStorage store, HostInformation hostInfo, string endpointName)
        {
            this.customChecks = customChecks;
            this.store = store;
            localEndpointDetails = new EndpointDetails
            {
                Host = hostInfo.DisplayName,
                HostId = hostInfo.HostId,
                Name = endpointName
            };
        }

        protected override Task OnStart(IMessageSession session)
        {
            foreach (var check in customChecks)
            {
                var checkManager = new InternalCustomCheckManager(store, check, localEndpointDetails);
                checkManager.Start();

                managers.Add(checkManager);
            }

            return Task.CompletedTask;
        }

        protected override async Task OnStop(IMessageSession session)
        {
            if (managers.Any())
            {
                await Task.WhenAll(managers.Select(m => m.Stop()).ToArray())
                    .ConfigureAwait(false);
            }
        }

        IList<ICustomCheck> customChecks;
        readonly CustomChecksStorage store;
        readonly EndpointDetails localEndpointDetails;
        IList<InternalCustomCheckManager> managers = new List<InternalCustomCheckManager>();
    }
}