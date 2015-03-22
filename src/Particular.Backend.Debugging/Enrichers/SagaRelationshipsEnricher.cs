namespace Particular.Backend.Debugging.Enrichers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Particular.Backend.Debugging.Api;
    using Particular.Operations.Ingestion.Api;

    public class SagaRelationshipsEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(IngestedMessage message, MessageSnapshot snapshot)
        {
            var headers = message.Headers;
            string sagasInvokedRaw;

            var invokedSagas = new List<SagaInfo>();
            if (headers.TryGet("NServiceBus.InvokedSagas", out sagasInvokedRaw))
            {
                string sagasChangeRaw;
                var sagasChanges = new Dictionary<string, string>();
                if (headers.TryGet("ServiceControl.SagaStateChange", out sagasChangeRaw))
                {
                    var multiSagaChanges = sagasChangeRaw.Split(';');

                    foreach (var part in multiSagaChanges.Select(s => s.Split(':')))
                    {
                        sagasChanges.Add(part[0], part[1]);
                    }
                }

                invokedSagas = sagasInvokedRaw.Split(';')
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
            }
            else
            {
                //for backwards compatibility
                string sagaId;
                string sagaType;
                if (headers.TryGet(NServiceBus.Headers.SagaId, out sagaId)
                    && headers.TryGet(NServiceBus.Headers.SagaType, out sagaType))
                {
                   invokedSagas = new List<SagaInfo>{new SagaInfo{SagaId = Guid.Parse(sagaId),SagaType = sagaType.Split(',').First()}};
                }
            }

            SagaInfo originatesFrom = null;
            string originatingSagaId;
            string originatingsagaType;
            if (headers.TryGet(NServiceBus.Headers.OriginatingSagaId, out originatingSagaId)
                && headers.TryGet(NServiceBus.Headers.OriginatingSagaType, out originatingsagaType))
            {
                originatesFrom = new SagaInfo { SagaId = Guid.Parse(originatingSagaId), SagaType = originatingsagaType.Split(',').First() };
            }

            snapshot.Sagas = new SagaInformation
            {
                InvokedSagas = invokedSagas,
                OriginatesFromSaga = originatesFrom
            };
        }
    }
}