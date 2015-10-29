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
            public override void Enrich(ImportMessage message)
            {
                string sagasInvokedRaw;

                if (message.PhysicalMessage.Headers.TryGetValue("NServiceBus.InvokedSagas", out sagasInvokedRaw))
                {
                    string sagasChangeRaw;
                    var sagasChanges = new Dictionary<string, string>();
                    if (message.PhysicalMessage.Headers.TryGetValue("ServiceControl.SagaStateChange", out sagasChangeRaw))
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

                            sagasChanges.TryGetValue(sagaInvoked[1], out changeText);

                            return new SagaInfo
                            {
                                SagaId = Guid.Parse(sagaInvoked[1]),
                                SagaType = sagaInvoked[0],
                                ChangeStatus = changeText,
                            };
                        })
                        .ToList();

                    message.Metadata.Add("InvokedSagas", sagas);
                }
                else
                {
                    string sagaId;

                    //for backwards compatibility
                    if (message.PhysicalMessage.Headers.TryGetValue(Headers.SagaId, out sagaId))
                    {
                        // A failure when a MarkAsComplete control message is received causes a saga message to be received in
                        // the error queue without a Headers.SagaType header.
                        // Hence the reason for the check!
                        string sagaType;
                        if (message.PhysicalMessage.Headers.TryGetValue(Headers.SagaType, out sagaType))
                        {
                            sagaType = sagaType.Split(',').First();
                        }
                        else
                        {
                            sagaType = "Unknown";
                        }

                        message.Metadata.Add("InvokedSagas", new List<SagaInfo>
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

                if (message.PhysicalMessage.Headers.TryGetValue(Headers.OriginatingSagaId, out originatingSagaId))
                {
                    // I am not sure if we need this logic here as well, but just in case see comment above.
                    string sagaType;
                    if (message.PhysicalMessage.Headers.TryGetValue(Headers.OriginatingSagaType, out sagaType))
                    {
                        sagaType = sagaType.Split(',').First();
                    }
                    else
                    {
                        sagaType = "Unknown";
                    }

                    message.Metadata.Add("OriginatesFromSaga", new SagaInfo
                    {
                        SagaId = Guid.Parse(originatingSagaId),
                        SagaType = sagaType
                    });
                }
            }
        }
    }
}