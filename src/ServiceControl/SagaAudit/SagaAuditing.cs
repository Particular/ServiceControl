﻿namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;

    public class SagaAuditing : Feature
    {
        public SagaAuditing()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<SagaRelationshipsEnricher>(DependencyLifecycle.SingleInstance);
        }

        internal class SagaRelationshipsEnricher : ErrorImportEnricher
        {
            public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var result = InvokedSagasParser.Parse(headers);
                if (result.OriginatesFromSaga != null)
                {
                    metadata.Add("OriginatesFromSaga", result.OriginatesFromSaga);
                }

                if (result.InvokedSagas != null)
                {
                    metadata.Add("InvokedSagas", result.InvokedSagas);
                }

                return Task.CompletedTask;
            }
        }
    }
}