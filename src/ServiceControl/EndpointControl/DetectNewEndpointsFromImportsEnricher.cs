namespace ServiceControl.EndpointControl.Handlers
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;
    using Particular.HealthMonitoring.Uptime;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Monitoring;

    public class EndpointDetectionFeature : Feature
    {
        public EndpointDetectionFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<DetectNewEndpointsFromImportsEnricher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<EnrichWithEndpointDetails>(DependencyLifecycle.SingleInstance);
        }

        class EnrichWithEndpointDetails : ImportEnricher
        {
            public override void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);
                // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
                // have the relevant information via the headers, which were added in v4.
                if (sendingEndpoint != null)
                {
                    metadata.Add("SendingEndpoint", sendingEndpoint);
                }

                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);
                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (receivingEndpoint != null)
                {
                    metadata.Add("ReceivingEndpoint", receivingEndpoint);
                }
            }
        }

        class DetectNewEndpointsFromImportsEnricher : ImportEnricher
        {
            EndpointInstanceMonitoring monitoring;
            MonitoringDataPersister persister;

            public DetectNewEndpointsFromImportsEnricher(UptimeMonitoring uptimeMonitoring)
            {
                this.monitoring = uptimeMonitoring.Monitoring;
            }

            public override void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);

                // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
                // have the relevant information via the headers, which were added in v4.
                if (sendingEndpoint != null)
                {
                    TryAddEndpoint(sendingEndpoint);
                }

                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);
                TryAddEndpoint(receivingEndpoint);
            }

            void TryAddEndpoint(EndpointDetails endpointDetails)
            {
                // SendingEndpoint will be null for messages that are from v3.3.x endpoints because we don't
                // have the relevant information via the headers, which were added in v4.
                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (endpointDetails == null)
                {
                    return;
                }

                // for backwards compat with version before 4_5 we might not have a hostid
                if (endpointDetails.HostId == Guid.Empty)
                {
                    return;
                }

                monitoring.GetOrCreateMonitor(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId, false);

                persister.RegisterEndpoint(new EndpointDetails
                {
                    Host = endpointDetails.Host,
                    Name = endpointDetails.Name,
                    HostId = endpointDetails.HostId
                });
            }
        }
    }
}