namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using Operations;

    public class SagaRelationshipsEnricher : ImportEnricher
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
                if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.SagaId, out sagaId))
                {
                    var sagaType = message.PhysicalMessage.Headers[NServiceBus.Headers.SagaType].Split(',').First();

                    message.Metadata.Add("InvokedSagas", new List<SagaInfo>{new SagaInfo{SagaId = Guid.Parse(sagaId),SagaType =sagaType}});
                }
            }


            string originatingSagaId;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.OriginatingSagaId, out originatingSagaId))
            {
                var sagaType = message.PhysicalMessage.Headers[NServiceBus.Headers.OriginatingSagaType].Split(',').First();

                message.Metadata.Add("OriginatesFromSaga", new SagaInfo { SagaId = Guid.Parse(originatingSagaId), SagaType = sagaType });
            }
        }
    }
}