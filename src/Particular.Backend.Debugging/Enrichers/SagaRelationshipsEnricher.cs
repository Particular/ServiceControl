namespace Particular.Backend.Debugging.Enrichers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Particular.Backend.Debugging.Api;
    using ServiceControl.Shell.Api.Ingestion;


    public class BodyEnricher : IEnrichAuditMessageSnapshots
    {
        
    }

    public class SagaRelationshipsEnricher : IEnrichAuditMessageSnapshots
    {
        public void Enrich(HeaderCollection headers, SnapshotMetadata metadata)
        {

            string sagasInvokedRaw;

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

                metadata.Set("InvokedSagas", sagas);
            }
            else
            {
                //for backwards compatibility
                string sagaId;
                string sagaType;
                if (headers.TryGet(NServiceBus.Headers.SagaId, out sagaId)
                    && headers.TryGet(NServiceBus.Headers.SagaType, out sagaType))
                {

                    metadata.Set("InvokedSagas", new List<SagaInfo>{new SagaInfo{SagaId = Guid.Parse(sagaId),SagaType = sagaType.Split(',').First()}});
                }
            }


            string originatingSagaId;
            string originatingsagaType;
            if (headers.TryGet(NServiceBus.Headers.OriginatingSagaId, out originatingSagaId)
                && headers.TryGet(NServiceBus.Headers.OriginatingSagaType, out originatingsagaType))
            {
                metadata.Set("OriginatesFromSaga", new SagaInfo { SagaId = Guid.Parse(originatingSagaId), SagaType = originatingsagaType.Split(',').First() });
            }
        }
    }
}