namespace ServiceControl.Shell.Infrastructure.Ingestion
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Shell.Api.Ingestion;

    class Ingestion : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            var ingestorTypes = Configure.TypesToScan.Where(x => !x.IsAbstract && typeof(MessageIngestor).IsAssignableFrom(x)).ToArray();

            foreach (var ingestorType in ingestorTypes)
            {
                Configure.Component(ingestorType, DependencyLifecycle.SingleInstance);
            }

            var ingestorRunnerTypes = ingestorTypes.Select(x => typeof(IngestorRunner<>).MakeGenericType(x));
            foreach (var runnerType in ingestorRunnerTypes)
            {
                Configure.Component(runnerType, DependencyLifecycle.SingleInstance);
            }
        }
    }
}