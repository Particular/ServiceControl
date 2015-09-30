using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Raven.Client.Embedded;
using ServiceControl.SagaAudit;
using ServiceControl.UnitTests.Expiration;

class Program
{
    static int numberOfSagaStateChangesToCreate = 20;
    static int numberOfSagaHistoriesPerDay = 10;
    static int numberOfDays = 1000;

    static void Main()
    {
        Console.WriteLine("Total: " + (numberOfDays * numberOfSagaHistoriesPerDay));
        var path = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault();
        if (path == null)
        {
            path = @"C:\ProgramData\Particular\ServiceControl\localhost-33333";
        }
        using (var documentStore = new EmbeddableDocumentStore
        {
            DataDirectory = path,
            EnlistInDistributedTransactions = false,
        })
        {
            documentStore.Configuration.CompiledIndexCacheDirectory = path;
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Initialize();
            var end = DateTime.Now.AddDays(-numberOfDays);
            var count = 0;
            for (var dateTime = DateTime.Now; dateTime > end; dateTime = dateTime.AddDays(-1))
            {
                using (new RavenLastModifiedScope(dateTime))
                using (var documentSession = documentStore.OpenSession())
                {
                    foreach (var sagaHistory in BuildSagaHistories())
                    {
                        count++;
                        if (count%50 == 0)
                        {
                            Console.WriteLine(count);
                        }
                        documentSession.Store(sagaHistory);
                    }
                    documentSession.SaveChanges();
                }
            }
        }
        Console.WriteLine("Done");
        if (Debugger.IsAttached)
        {
            Console.ReadLine();
        }
    }

    static IEnumerable<SagaHistory> BuildSagaHistories()
    {
        for (var i = 0; i < numberOfSagaHistoriesPerDay; i++)
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