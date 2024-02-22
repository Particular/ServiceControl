namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;
    using ServiceControl.Operations;

    class ProcessingStatisticsEnricher : IEnrichImportedErrorMessages
    {
        public void Enrich(ErrorEnricherContext context)
        {
            if (!context.Headers.TryGetValue(Headers.TimeSent, out var timeSentValue))
            {
                return;
            }

            var timeSent = DateTimeOffsetHelper.ToDateTimeOffset(timeSentValue).UtcDateTime;
            context.Metadata.Add("TimeSent", timeSent);
        }
    }
}