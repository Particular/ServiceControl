namespace ServiceControl.EndpointControl.Handlers
{
    using System;
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

        class DetectNewEndpointsFromErrorImportsEnricher : IEnrichImportedErrorMessages
        {
            public DetectNewEndpointsFromErrorImportsEnricher(EndpointInstanceMonitoring monitoring)
            {
                this.monitoring = monitoring;
            }

            public void Enrich(ErrorEnricherContext context)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(context.Headers);

                // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
                // have the relevant information via the headers, which were added in v4.
                if (sendingEndpoint != null)
                {
                    TryAddEndpoint(sendingEndpoint, context);
                    context.Metadata.Add("SendingEndpoint", sendingEndpoint);
                }

                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(context.Headers);
                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (receivingEndpoint != null)
                {
                    context.Metadata.Add("ReceivingEndpoint", receivingEndpoint);
                    TryAddEndpoint(receivingEndpoint, context);
                }
            }

            void TryAddEndpoint(EndpointDetails endpointDetails, ErrorEnricherContext context)
            {
                // for backwards compat with version before 4_5 we might not have a hostid
                if (endpointDetails.HostId == Guid.Empty)
                {
                    return;
                }

                if (monitoring.IsNewInstance(endpointDetails))
                {
                    context.Add(endpointDetails);
                }
            }

            EndpointInstanceMonitoring monitoring;
        }
    }
}