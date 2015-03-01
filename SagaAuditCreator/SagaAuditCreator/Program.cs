using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Raven.Client.Embedded;
using ServiceControl.SagaAudit;

class Program
{
    static int numberOfSagaStateChangesToCreate = 20;
    static int numberOfSagaHistoriesToCreate = 1000;
    static void Main()
    {
        var path = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault();
        if (path == null)
        {
            path = "C:\\ProgramData\\Particular\\ServiceControl\\localhost-33333";
        }
        using (var documentStore = new EmbeddableDocumentStore
                                   {
                                       DataDirectory = path, EnlistInDistributedTransactions = false,
                                   })
        {
            documentStore.Configuration.CompiledIndexCacheDirectory = path;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Initialize();

            var blockSize = 50;
            var total = 0;
            foreach (var batch in BuildSagaHistories().Select((x, index) => new {x, index }).GroupBy(x => x.index/blockSize, y => y.x))
            {
                total += blockSize;
                Console.WriteLine(total);
                using (var documentSession = documentStore.OpenSession())
                {
                    foreach (var sagaHistory in batch)
                    {
                        documentSession .Store(sagaHistory);
                    }
                    documentSession.SaveChanges();
                }
            }
        }
        if (Debugger.IsAttached)
        {
            Console.ReadLine();
        }
    }

    static IEnumerable<SagaHistory> BuildSagaHistories()
    {
        for (var i = 0; i < numberOfSagaHistoriesToCreate; i++)
        {
            yield return new SagaHistory
                         {
                             SagaId = Guid.NewGuid(),
                             SagaType = "MySaga1",
                             Changes = BuildStateChanges().ToList()
                         };
        }

    }

    static IEnumerable<SagaStateChange> BuildStateChanges()
    {
        for (var i = 0; i < numberOfSagaStateChangesToCreate ; i++)
        {
            yield return new SagaStateChange
                         {
                             Endpoint = "MyEndpoint",
                             StartTime = DateTime.Now,
                             FinishTime = DateTime.Now,
                             Status = SagaStateChangeStatus.Updated,
                             StateAfterChange = "Completed",
                             InitiatingMessage = new InitiatingMessage
                                                 {
                                                     Intent = "Send",
                                                     IsSagaTimeoutMessage = false,
                                                     MessageId = "1",
                                                     MessageType = "MyMessage1",
                                                     OriginatingEndpoint = "Endpoint1",
                                                     OriginatingMachine = "Machine1",
                                                     TimeSent = DateTime.Now
                                                 },
                             OutgoingMessages = new List<ResultingMessage>
                                                {
                                                    new ResultingMessage
                                                    {
                                                        DeliverAt = DateTime.Now,
                                                        DeliveryDelay = TimeSpan.FromMinutes(2),
                                                        Destination = "Endpoint2",
                                                        Intent = "Send",
                                                        MessageId = "2",
                                                        TimeSent = DateTime.Now,
                                                        MessageType = "MyMessage2",
                                                    }
                                                }
                         };
        }
    }
}