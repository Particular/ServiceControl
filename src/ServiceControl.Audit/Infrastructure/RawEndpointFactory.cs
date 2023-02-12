﻿namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using NServiceBus.Raw;
    using Transports;

    class RawEndpointFactory
    {
        public RawEndpointFactory(TransportSettings transportSettings, TransportCustomization transportCustomization)
        {
            this.transportSettings = transportSettings;
            this.transportCustomization = transportCustomization;
        }

        public RawEndpointConfiguration CreateRawEndpointToProvisionAuditQueues(string auditQueue)
        {
            var config = RawEndpointConfiguration.Create(auditQueue, (_, __) => throw new NotImplementedException(), $"{transportSettings.EndpointName}.Errors");

            transportCustomization.CustomizeForQueueIngestion(config, transportSettings);
            return config;
        }

        public RawEndpointConfiguration CreateRawSendOnlyEndpoint(string name)
        {
            var config = RawEndpointConfiguration.CreateSendOnly(name);

            transportCustomization.CustomizeRawSendOnlyEndpoint(config, transportSettings);
            return config;
        }

        TransportCustomization transportCustomization;
        TransportSettings transportSettings;
    }
}