namespace ServiceControl.MessageAuditing
{
    using System;
    using Infrastructure.Messages;
    using NServiceBus;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceBus.Management.MessageAuditing;

    class AuditMessageHandler : IHandleMessages<AuditMessageReceived>
    {
        public IDocumentStore Store { get; set; }
        
        public void Handle(AuditMessageReceived message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var auditMessage = new ServiceBus.Management.MessageAuditing.Message(message.MessageDetails);

                auditMessage.MarkAsSuccessful(message.MessageDetails);

                try
                {
                    session.Store(auditMessage);

                    session.SaveChanges();
                }
                catch (ConcurrencyException)
                {
                    session.Advanced.Clear();
                    UpdateExistingMessage(session, auditMessage.Id, message.MessageDetails);
                }
            }
        }

        void UpdateExistingMessage(IDocumentSession session, string messageId, ITransportMessage message)
        {
            var auditMessage = session.Load<Message>(messageId);

            if (auditMessage == null)
            {
                throw new InvalidOperationException("There should be a message in the store");
            }

            auditMessage.Update(message);

            session.SaveChanges();
        }
    }
}
