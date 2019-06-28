﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using NServiceBus;
    using NServiceBus.Features;

    class ProcessingStatistics : Feature
    {
        public ProcessingStatistics()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ProcessingStatisticsEnricher>(DependencyLifecycle.SingleInstance);
        }

        class ProcessingStatisticsEnricher : IEnrichImportedAuditMessages
        {
            public void Enrich(AuditEnricherContext context)
            {
                var headers = context.Headers;
                var metadata = context.Metadata;
                var processingEnded = DateTime.MinValue;
                var timeSent = DateTime.MinValue;
                var processingStarted = DateTime.MinValue;

                if (headers.TryGetValue(Headers.TimeSent, out var timeSentValue))
                {
                    timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                    metadata.Add("TimeSent", timeSent);
                }

                if (headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue))
                {
                    processingStarted = DateTimeExtensions.ToUtcDateTime(processingStartedValue);
                }

                if (headers.TryGetValue(Headers.ProcessingEnded, out var processingEndedValue))
                {
                    processingEnded = DateTimeExtensions.ToUtcDateTime(processingEndedValue);
                }

                var criticalTime = TimeSpan.Zero;

                if (processingEnded != DateTime.MinValue && timeSent != DateTime.MinValue)
                {
                    criticalTime = processingEnded - timeSent;
                }

                metadata.Add("CriticalTime", criticalTime);

                var processingTime = TimeSpan.Zero;

                if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
                {
                    processingTime = processingEnded - processingStarted;
                }

                metadata.Add("ProcessingTime", processingTime);

                var deliveryTime = TimeSpan.Zero;

                if (processingStarted != DateTime.MinValue && timeSent != DateTime.MinValue)
                {
                    deliveryTime = processingStarted - timeSent;
                }

                metadata.Add("DeliveryTime", deliveryTime);
            }
        }
    }
}