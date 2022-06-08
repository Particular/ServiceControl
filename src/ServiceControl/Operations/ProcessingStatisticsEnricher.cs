namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;
    using ServiceControl.Operations;

    class ProcessingStatisticsEnricher : IEnrichImportedErrorMessages
    {
        public void Enrich(ErrorEnricherContext context)
        {
            if (context.Headers.TryGetValue(Headers.TimeSent, out var timeSentValue))
            {
                var timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                context.Metadata.Add("TimeSent", timeSent);
            }
        }
    }
}