namespace ServiceControl.MessageAuditing
{
    using System;
    using Contracts.Operations;
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

                var auditMessage = new Message(message);

                auditMessage.MarkAsSuccessful(message.Headers);

                try
                {
                    session.Store(auditMessage);
                    session.SaveChanges();
                }
                catch (ConcurrencyException)
                {
                    session.Advanced.Clear();
                    UpdateExistingMessage(session, auditMessage.Id, message);
                }
            }
        }

        void UpdateExistingMessage(IDocumentSession session, string messageId, AuditMessageReceived message)
        {
            var auditMessage = session.Load<Message>(messageId);

            if (auditMessage == null)
            {
                throw new InvalidOperationException("There should be a message in the store");
            }

            auditMessage.Update(message.Body, message.Headers);

            session.SaveChanges();
        }
    }
}
