namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;

    public class ProcessingStatistics : Feature
    {
        public ProcessingStatistics()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ProcessingStatisticsEnricher>(DependencyLifecycle.SingleInstance);
        }

        class ProcessingStatisticsEnricher : ImportEnricher
        {
            public override void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var processingEnded = DateTime.MinValue;
                var timeSent = DateTime.MinValue;
                var processingStarted = DateTime.MinValue;

                string timeSentValue;

                if (headers.TryGetValue(Headers.TimeSent, out timeSentValue))
                {
                    timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                    metadata.Add("TimeSent", timeSent);
                }

                string processingStartedValue;

                if (headers.TryGetValue(Headers.ProcessingStarted, out processingStartedValue))
                {
                    processingStarted = DateTimeExtensions.ToUtcDateTime(processingStartedValue);
                }

                string processingEndedValue;

                if (headers.TryGetValue(Headers.ProcessingEnded, out processingEndedValue))
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