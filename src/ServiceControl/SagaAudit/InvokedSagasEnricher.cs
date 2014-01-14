namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus.Scheduling.Messages;
    using Operations;

    public class InvokedSagasEnricher:ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {

            string sagasInvokedHeader;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.InvokedSagas, out sagasInvokedHeader))
            {
                var sagas = sagasInvokedHeader.Split(';')
                    .Select(saga => new SagaInfo{ SagaType =  saga.Split(':')[0], SagaId = Guid.Parse( saga.Split(':')[1])})
                    .ToList();

                message.Metadata.Add("SagasInvoked",sagas);

                return;
            }


            string sagaIdHeader;


            //for backwards compatibility
            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.SagaId, out sagaIdHeader))
            {
                var sagaType = message.PhysicalMessage.Headers[NServiceBus.Headers.SagaType].Split(',').First();

                message.Metadata.Add("SagasInvoked", new List<SagaInfo>
                {
                    new SagaInfo{SagaId = Guid.Parse(sagaIdHeader),SagaType =sagaType}
                });
            }
        }

        bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains(typeof(ScheduledTask).FullName);
        }

        string GetMessageType(string messageTypeString)
        {
            if (!messageTypeString.Contains(","))
            {
                return messageTypeString;
            }

            return messageTypeString.Split(',').First();
        }
    }
}