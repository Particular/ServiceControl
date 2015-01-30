namespace Particular.Backend.Debugging.Enrichers
{
    using System;
    using NServiceBus;
    using ServiceControl.Shell.Api.Ingestion;

    public class ProcessingStatisticsEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(HeaderCollection headers, SnapshotMetadata metadata)
        {
            var processingEnded = DateTime.MinValue;
            var timeSent = DateTime.MinValue;
            var processingStarted = DateTime.MinValue;

            string timeSentValue;

            if (headers.TryGet(Headers.TimeSent, out timeSentValue))
            {
                timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                metadata.Set("TimeSent", timeSent);
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

            metadata.Set("CriticalTime", criticalTime);

            var processingTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
            {
                processingTime = processingEnded - processingStarted;
            }

            metadata.Set("ProcessingTime", processingTime);

            var deliveryTime = TimeSpan.Zero;

            if (processingStarted != DateTime.MinValue && timeSent != DateTime.MinValue)
            {
                deliveryTime = processingStarted - timeSent;
            }

            metadata.Set("DeliveryTime", deliveryTime);
        }
    }
}