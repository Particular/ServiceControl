namespace ImportMessagesToQueue
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Transports.Msmq;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceControl.MessageAuditing;

    class Program
    {
        static string SOURCE_DB_PATH = @"C:\ProgramData\Particular\ServiceControl\nsbscstg-H1-33333";

        static void Main()
        {
            var messageQueue = new MessageQueue(@".\Private$\audit", false, true, QueueAccessMode.Send);
            const int pageSize = 1000;


            using (var raven = GetRaven(SOURCE_DB_PATH))
            {
                for (int iteration = 0; iteration < 4; iteration++)
                {
                    for (int page = 1; page < 76; page++)
                    {
                        using (var session = raven.OpenSession())
                        {
                            var messages = session.Query<ProcessedMessage>()
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .OrderByDescending(m => m.ProcessedAt)
                                .ToArray();

                            InsertMessageToQueue(messageQueue, messages);
                            session.Advanced.Clear();
                        }
                        Console.WriteLine("Messages added to the queue - page " + (iteration * pageSize + page) + " out of 150.");

                    }
                }
            }


            Console.WriteLine("All messages added to the queue.");
            Console.ReadKey();
        }

        static void InsertMessageToQueue(MessageQueue messageQueue, IEnumerable<ProcessedMessage> messages)
        {
            using (var myTransaction = new MessageQueueTransaction())
            {
                myTransaction.Begin();
                foreach (var message in messages)
                {
                    object body;
                    message.MessageMetadata.TryGetValue("Body", out body);
                    if (body == null)
                        body = "<?xml version=\"1.0\"?><ScheduledTask xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://tempuri.net/NServiceBus.Scheduling.Messages\"><TaskId>5502e17a-a79f-4328-8445-3285f9af9403</TaskId><Name>Scheduler</Name><Every>PT15M</Every></ScheduledTask>";
                    
                    var rawMessage = new Message
                    {
                        CorrelationId = message.Headers[Headers.CorrelationId] + "\\0",
                        Extension = SerializeMessageHeaders(message.Headers.Select(x => new HeaderInfo()
                        {
                            Key = x.Key,
                            Value = x.Value
                        }).ToList()),
                        BodyStream = new MemoryStream(Encoding.UTF8.GetBytes((string)body))
                    };

                    messageQueue.Send(rawMessage, myTransaction);
                }
                myTransaction.Commit();
            }
        }

        static byte[] SerializeMessageHeaders(IEnumerable<HeaderInfo> headers)
        {
            var headerSerializer = new System.Xml.Serialization.XmlSerializer(typeof(List<HeaderInfo>));
            using (var stream = new MemoryStream())
            {
                headerSerializer.Serialize(stream, headers.ToList());
                return stream.ToArray();
            }
        }

        public static IDocumentStore GetRaven(string dbPath)
        {
            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = dbPath,
                UseEmbeddedHttpServer = false,
                EnlistInDistributedTransactions = false,
            };

            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.xml");
            if (File.Exists(localRavenLicense))
            {
                documentStore.Configuration.Settings["Raven/License"] = NonLockingFileReader.ReadAllTextWithoutLocking(localRavenLicense);
            }

            documentStore.Configuration.Settings.Add("Raven/ActiveBundles", "CustomDocumentExpiration");

            documentStore.Configuration.Port = 33333;
            documentStore.Configuration.HostName = "localhost";
            documentStore.Configuration.CompiledIndexCacheDirectory = dbPath;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Initialize();

            return documentStore;
        }

        static class NonLockingFileReader
        {
            public static string ReadAllTextWithoutLocking(string path)
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var textReader = new StreamReader(fileStream))
                {
                    return textReader.ReadToEnd();
                }
            }
        }
    }
}
