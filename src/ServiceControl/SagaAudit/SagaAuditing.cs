namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
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

        class SagaRelationshipsEnricher : ImportEnricher
        {
            public override void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                string sagasInvokedRaw;

                if (headers.TryGetValue("NServiceBus.InvokedSagas", out sagasInvokedRaw))
                {
                    string sagasChangeRaw;
                    var sagasChanges = new Dictionary<string, string>();
                    if (headers.TryGetValue("ServiceControl.SagaStateChange", out sagasChangeRaw))
                    {
                        var multiSagaChanges = sagasChangeRaw.Split(';');

                        foreach (var part in multiSagaChanges.Select(s => s.Split(':')))
                        {
                            sagasChanges.Add(part[0], part[1]);
                        }
                    }

                    var sagas = sagasInvokedRaw.Split(';')
                        .Select(saga =>
                        {
                            var sagaInvoked = saga.Split(':');
                            string changeText;

                            var guid = sagaInvoked[1];
                            sagasChanges.TryGetValue(guid, out changeText);

                            return new SagaInfo
                            {
                                SagaId = Guid.Parse(guid),
                                SagaType = sagaInvoked[0],
                                ChangeStatus = changeText,
                            };
                        })
                        .ToList();

                    metadata.Add("InvokedSagas", sagas);
                }
                else
                {
                    string sagaId;

                    //for backwards compatibility
                    if (headers.TryGetValue(Headers.SagaId, out sagaId))
                    {
                        // A failure when a MarkAsComplete control message is received causes a saga message to be received in
                        // the error queue without a Headers.SagaType header.
                        // Hence the reason for the check
                        string sagaType;
                        if (headers.TryGetValue(Headers.SagaType, out sagaType))
                        {
                            sagaType = sagaType.Split(',').First();
                        }
                        else
                        {
                            sagaType = "Unknown";
                        }

                        metadata.Add("InvokedSagas", new List<SagaInfo>
                        {
                            new SagaInfo
                            {
                                SagaId = Guid.Parse(sagaId),
                                SagaType = sagaType
                            }
                        });
                    }
                }

                string originatingSagaId;

                if (headers.TryGetValue(Headers.OriginatingSagaId, out originatingSagaId))
                {
                    // I am not sure if we need this logic here as well, but just in case see comment above.
                    string sagaType;
                    if (headers.TryGetValue(Headers.OriginatingSagaType, out sagaType))
                    {
                        sagaType = sagaType.Split(',').First();
                    }
                    else
                    {
                        sagaType = "Unknown";
                    }

                    metadata.Add("OriginatesFromSaga", new SagaInfo
                    {
                        SagaId = Guid.Parse(originatingSagaId),
                        SagaType = sagaType
                    });
                }
            }
        }
    }
}