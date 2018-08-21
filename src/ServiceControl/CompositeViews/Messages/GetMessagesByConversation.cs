namespace ServiceControl.CompositeViews.Messages
{
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetMessagesByConversation : BaseModule
    {
        public GetMessagesByConversation()
        {
            Get["/conversations/{conversationid}", true] = (parameters, token) =>
            {
                string conversationId = parameters.conversationid;

                return MessagesByConversationApi.Execute(this, conversationId);
            };
        }

        public MessagesByConversationApi MessagesByConversationApi { get; set; }
    }
}