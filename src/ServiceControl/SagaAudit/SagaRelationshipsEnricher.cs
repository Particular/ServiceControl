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

            string sagasInvokedHeader;

            if (message.PhysicalMessage.Headers.TryGetValue("NServiceBus.InvokedSagas", out sagasInvokedHeader))
            {
                var sagas = sagasInvokedHeader.Split(';')
                    .Select(saga => new SagaInfo { SagaType = saga.Split(':')[0], SagaId = Guid.Parse(saga.Split(':')[1]) })
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