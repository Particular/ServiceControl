namespace ServiceControl.Audit.Auditing
{
    using System;
    using NServiceBus;
    class ProcessingStatisticsEnricher : IEnrichImportedAuditMessages
    {
        public void Enrich(AuditEnricherContext context)
        {
            var headers = context.Headers;
            var metadata = context.Metadata;
            var processingEnded = DateTime.MinValue;
            var startTime = DateTime.MinValue;
            var processingStarted = DateTime.MinValue;

            if (headers.TryGetValue(Headers.TimeSent, out var timeSentValue))
            {
                startTime = DateTimeOffsetHelper.ToDateTimeOffset(timeSentValue).UtcDateTime;
                metadata.Add("TimeSent", startTime);
            }

            if (headers.TryGetValue(Headers.DeliverAt, out var deliverAtValue))
            {
                startTime = DateTimeOffsetHelper.ToDateTimeOffset(deliverAtValue).UtcDateTime;
            }

            if (headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue))
            {
                processingStarted = DateTimeOffsetHelper.ToDateTimeOffset(processingStartedValue).UtcDateTime;
            }

            if (headers.TryGetValue(Headers.ProcessingEnded, out var processingEndedValue))
            {
                processingEnded = DateTimeOffsetHelper.ToDateTimeOffset(processingEndedValue).UtcDateTime;
            }

            var criticalTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && startTime != DateTime.MinValue)
            {
                criticalTime = processingEnded - startTime;
            }

            metadata.Add("CriticalTime", criticalTime);

            var processingTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
            {
                processingTime = processingEnded - processingStarted;
            }

            metadata.Add("ProcessingTime", processingTime);

            var deliveryTime = TimeSpan.Zero;

            if (processingStarted != DateTime.MinValue && startTime != DateTime.MinValue)
            {
                deliveryTime = processingStarted - startTime;
            }

            metadata.Add("DeliveryTime", deliveryTime);
        }
    }
}