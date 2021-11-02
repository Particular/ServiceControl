namespace NServiceBus
{
    using System;
    using System.Collections.Generic;

    public class ServicePlatformSagaAuditConfiguration
    {
        public string SagaAuditQueue { get; set; }
        public Func<object, Dictionary<string, string>> CustomSagaEntitySerialization { get; set; }

        internal void ApplyTo(EndpointConfiguration endpointConfiguration)
        {
            if (string.IsNullOrWhiteSpace(SagaAuditQueue) == false)
            {
                endpointConfiguration.AuditSagaStateChanges(SagaAuditQueue, CustomSagaEntitySerialization);
            }
        }
    }
}