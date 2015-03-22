namespace Particular.Backend.Debugging.Enrichers
{
    using System;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;

    public class ProcessingStatisticsEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, MessageSnapshot snapshot)
        {
            var headers = message.Headers;
            var processingEnded = DateTime.MinValue;
            var timeSent = DateTime.MinValue;
            var processingStarted = DateTime.MinValue;

            string timeSentValue;

            if (headers.TryGet(Headers.TimeSent, out timeSentValue))
            {
                timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
            }

            string processingStartedValue;

            if (headers.TryGet(Headers.ProcessingStarted, out processingStartedValue))
            {
                processingStarted = DateTimeExtensions.ToUtcDateTime(processingStartedValue);
            }

            string processingEndedValue;

            if (headers.TryGet(Headers.ProcessingEnded, out processingEndedValue))
            {
                processingEnded = DateTimeExtensions.ToUtcDateTime(processingEndedValue);
            }

            var criticalTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && timeSent != DateTime.MinValue)
            {
                criticalTime = processingEnded - timeSent;
            }

            var processingTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
            {
                processingTime = processingEnded - processingStarted;
            }

            var deliveryTime = TimeSpan.Zero;

            if (processingStarted != DateTime.MinValue && timeSent != DateTime.MinValue)
            {
                deliveryTime = processingStarted - timeSent;
            }

            snapshot.Processing = new ProcessingStatistics
            {
                TimeSent = timeSent,
                CriticalTime = criticalTime,
                ProcessingTime = processingTime,
                DeliveryTime = deliveryTime
            };
        }
    }
}