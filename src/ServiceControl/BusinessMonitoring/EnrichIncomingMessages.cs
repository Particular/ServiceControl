namespace ServiceControl.BusinessMonitoring
{
    using Contracts.Operations;
    using Operations;

    public class EnrichIncomingMessages : ImportEnricher
    {
    

        public override void Enrich(ImportMessage message)
        {
            string conversationId;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.ConversationId, out conversationId))
            {
                message.Add(new MessageProperty("ConversationId", conversationId));
            }

        }
    }

    
}