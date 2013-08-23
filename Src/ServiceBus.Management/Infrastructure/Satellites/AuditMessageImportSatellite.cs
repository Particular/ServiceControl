namespace ServiceBus.Management.Infrastructure.Satellites
{
    using System;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using Settings;

    public class AuditMessageImportSatellite : ISatellite
    {
        public IDocumentStore Store { get; set; }

        public bool Handle(TransportMessage message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var auditMessage = new Message(message);

                auditMessage.MarkAsSuccessful(message);

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

            return true;
        }

        public void Start()
        {
            Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.AuditQueue; }
        }

        public bool Disabled
        {
            get { return InputAddress == Address.Undefined; }
        }

        void UpdateExistingMessage(IDocumentSession session, string messageId, TransportMessage message)
        {
            var auditMessage = session.Load<Message>(messageId);

            if (auditMessage == null)
            {
                throw new InvalidOperationException("There should be a message in the store");
            }


            auditMessage.Update(message);

            session.SaveChanges();
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditMessageImportSatellite));
    }
}