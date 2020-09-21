namespace ServiceControl.Audit.Auditing
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
                var timeSent = DateTime.MinValue;

                if (headers.TryGetValue(Headers.TimeSent, out var timeSentValue))
                {
                    timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                    metadata.TimeSent = timeSent;
                }

                var processingStarted = headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue) 
                    ? DateTimeExtensions.ToUtcDateTime(processingStartedValue) 
                    : DateTime.MinValue;

                var processingEnded = headers.TryGetValue(Headers.ProcessingEnded, out var processingEndedValue) 
                    ? DateTimeExtensions.ToUtcDateTime(processingEndedValue) 
                    : DateTime.MinValue;

                metadata.CriticalTime = processingEnded != DateTime.MinValue && timeSent != DateTime.MinValue
                    ? processingEnded - timeSent
                    : TimeSpan.Zero;

                metadata.ProcessingTime = processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue
                    ? processingEnded - processingStarted
                    : TimeSpan.Zero;

                metadata.DeliveryTime = processingStarted != DateTime.MinValue && timeSent != DateTime.MinValue
                    ? processingStarted - timeSent
                    : TimeSpan.Zero;
            }
        }
    }
}