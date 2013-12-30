namespace ServiceControl.MessageAuditing
{
    using System;
    using Contracts.Operations;
    using NServiceBus;
    using Operations;

    public class ProcessingStatisticsEnricher : ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            var processingEnded = DateTime.MinValue;
            var timeSent = DateTime.MinValue;
            var processingStarted = DateTime.MinValue;

            string timeSentValue;

            if (message.PhysicalMessage.Headers.TryGetValue(Headers.TimeSent, out timeSentValue))
            {
                timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                message.Metadata.Add("TimeSent", timeSent);
            }


            string processingStartedValue;

            if (message.PhysicalMessage.Headers.TryGetValue(Headers.ProcessingStarted, out processingStartedValue))
            {
                processingStarted = DateTimeExtensions.ToUtcDateTime(processingStartedValue);
            }


            string processingEndedValue;

            if (message.PhysicalMessage.Headers.TryGetValue(Headers.ProcessingEnded, out processingEndedValue))
            {
                processingEnded = DateTimeExtensions.ToUtcDateTime(processingEndedValue);
            }

            var criticalTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && timeSent != DateTime.MinValue)
            {
                criticalTime = processingEnded - timeSent;
            }
            
            message.Metadata.Add("CriticalTime",criticalTime);

            var processingTime = TimeSpan.Zero;

            if (processingEnded != DateTime.MinValue && processingStarted != DateTime.MinValue)
            {
                processingTime = processingEnded - processingStarted;
            }

            message.Metadata.Add("ProcessingTime",processingTime);
            
        }
    }
}