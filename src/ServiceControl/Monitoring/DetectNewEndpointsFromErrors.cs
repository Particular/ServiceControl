namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;
    using ServiceControl.Contracts.Operations;

    class DetectNewEndpointsFromErrors : Feature
    {
        public DetectNewEndpointsFromErrors()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DetectNewEndpointsFromErrorImportsEnricher>(DependencyLifecycle.SingleInstance);
        }

        class DetectNewEndpointsFromErrorImportsEnricher : ErrorImportEnricher
        {
            public DetectNewEndpointsFromErrorImportsEnricher(EndpointInstanceMonitoring monitoring)
            {
                this.monitoring = monitoring;
            }

            public override async Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);

                // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
                // have the relevant information via the headers, which were added in v4.
                if (sendingEndpoint != null)
                {
                    await TryAddEndpoint(sendingEndpoint)
                        .ConfigureAwait(false);
                    metadata.Add("SendingEndpoint", sendingEndpoint);
                }

                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);
                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (receivingEndpoint != null)
                {
                    metadata.Add("ReceivingEndpoint", receivingEndpoint);
                    await TryAddEndpoint(receivingEndpoint)
                        .ConfigureAwait(false);
                }
            }

            async Task TryAddEndpoint(EndpointDetails endpointDetails)
            {
                // for backwards compat with version before 4_5 we might not have a hostid
                if (endpointDetails.HostId == Guid.Empty)
                {
                    return;
                }

                await monitoring.EndpointDetected(endpointDetails)
                    .ConfigureAwait(false);
            }

            private EndpointInstanceMonitoring monitoring;
        }
    }
}