using System;
using Raven.Client.Document;

namespace ResetMessageRetry
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Out.WriteLine("You need to supply the id of the message to reset.");
                return 1;
            }

            var store = new DocumentStore
            {
                Url = (args.Length == 2 ? args[1] : "http://localhost:33333/storage"),
            };

            store.Initialize();
            store.Conventions.MaxNumberOfRequestsPerSession = 2048;
            store.Conventions.SaveEnumsAsIntegers = true;

            Console.Out.WriteLine("Resetting message...");
            ResetMessages(store, args[0]);
            Console.Out.WriteLine("Done");

            return 0;
        }

        static void ResetMessages(DocumentStore store, string messageUniqueId)
        {
            using (var session = store.OpenSession())
            {
                var failedMessage = session.Load<FailedMessage>(FailedMessage.MakeDocumentId(messageUniqueId));
                if (failedMessage != null)
                {
                    failedMessage.Status = FailedMessageStatus.Unresolved;
                }

                var failedMessageRetry = session.Load<FailedMessageRetry>(FailedMessageRetry.MakeDocumentId(messageUniqueId));
                if (failedMessageRetry != null)
                {
                    session.Delete(failedMessageRetry);
                }

                session.SaveChanges();
            }
        }
    }
}
