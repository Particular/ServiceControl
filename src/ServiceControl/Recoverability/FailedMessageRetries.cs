namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.IdGeneration;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Indexes;
    using Raven.Client.Linq;
    using Raven.Database.Util;
    using Raven.Json.Linq;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;

    public class FailedMessageRetries : Feature
    {
        public override bool IsEnabledByDefault { get { return true; } }

        public FailedMessageRetries()
        {
            Configure.Component<RetryDocumentManager>(DependencyLifecycle.SingleInstance);
            Configure.Component<RetriesGateway>(DependencyLifecycle.SingleInstance);
        }
    }

    public class RetryBatch
    {
        public string Id { get; set; }
        public string RetrySessionId { get; set; }
        public RetryBatchStatus Status { get; set; }
        public IList<string> FailureRetries { get; set; }

        public RetryBatch()
        {
            FailureRetries = new List<string>();
        }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return "RetryBatches/" + messageUniqueId;
        }
    }

    public enum RetryBatchStatus
    {
        MarkingDocuments = 1,
        Staging = 2,
        Forwarding = 3
    }

    public class RetryDocumentManager
    {
        public IDocumentStore Store { get; set; }

        static string RetrySessionId = CombGuid.Generate().ToString();

        public string CreateBatchDocument()
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(CombGuid.Generate().ToString());
            using (var session = Store.OpenSession())
            {
                session.Store(new RetryBatch
                {
                    Id = batchDocumentId, 
                    RetrySessionId = RetrySessionId, 
                    Status = RetryBatchStatus.MarkingDocuments
                });
                session.SaveChanges();
            }
            return batchDocumentId;
        }

        public string CreateFailedMessageRetryDocument(string batchDocumentId, string messageUniqueId)
        {
            var failureRetryId = FailedMessageRetry.MakeDocumentId(messageUniqueId);
            Store.DatabaseCommands.Patch(failureRetryId,
                new PatchRequest[0], // if existing do nothing
                new[]
                {
                    new PatchRequest
                    {
                        Name = "FailedMessageId",
                        Type = PatchCommandType.Set,
                        Value = FailedMessage.MakeDocumentId(messageUniqueId)
                    }, 
                    new PatchRequest
                    {
                        Name = "RetryBatchId", 
                        Type = PatchCommandType.Set, 
                        Value = batchDocumentId
                    }, 
                },
                RavenJObject.Parse(String.Format(@"
                                    {{
                                        ""Raven-Entity-Name"": ""{0}"", 
                                        ""Raven-Clr-Type"": ""{1}""
                                    }}", FailedMessageRetry.CollectionName, 
                                       typeof(FailedMessageRetry).AssemblyQualifiedName))
                );
            return failureRetryId;
        }

        public void MoveBatchToStaging(string batchDocumentId, string[] failedMessageRetryIds)
        {
            Store.DatabaseCommands.Patch(batchDocumentId,
                new[]
                {
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set, 
                        Name = "Status", 
                        Value = (int)RetryBatchStatus.Staging, 
                        PrevVal = (int)RetryBatchStatus.MarkingDocuments
                    }, 
                    new PatchRequest
                    {
                        Type = PatchCommandType.Set, 
                        Name = "FailureRetries", 
                        Value = new RavenJArray((IEnumerable)failedMessageRetryIds)
                    }
                });
        }

        public void RemoveFailedMessageRetryDocument(string uniqueMessageId)
        {
            Store.DatabaseCommands.Delete(FailedMessage.MakeDocumentId(uniqueMessageId), null);
        }

        internal void AdoptOrphanedBatches()
        {
            using (var session = Store.OpenSession())
            {
                var orphanedBatchIds = session.Query<RetryBatch, RetryBatches_ByStatusAndSession>()
                    .Customize(q => q.WaitForNonStaleResultsAsOfNow())
                    .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != RetrySessionId)
                    .Select(b => b.Id)
                    .ToArray();

                AdoptBatches(session, orphanedBatchIds);
            }
        }

        void AdoptBatches(IDocumentSession session, string[] batchIds)
        {
            Parallel.ForEach(batchIds, batchId => AdoptBatch(session, batchId));
        }

        void AdoptBatch(IDocumentSession session, string batchId)
        {
            var query = session.Query<FailedMessageRetry, FailedMessageRetries_ByBatch>()
                .Where(r => r.RetryBatchId == batchId);

            var messageIds = new List<string>();

            using (var stream = session.Advanced.Stream(query))
            {
                while (stream.MoveNext())
                {
                    messageIds.Add(stream.Current.Document.Id);
                }
            }

            MoveBatchToStaging(batchId, messageIds.ToArray());
        }
    }

    public class FailedMessageRetry
    {
        public const string CollectionName = "FailedMessageRetries";

        public static string MakeDocumentId(string messageUniqueId)
        {
            return CollectionName + "/" + messageUniqueId;
        }

        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }
    }

    public class FailedMessageRetries_ByBatch : AbstractIndexCreationTask<FailedMessageRetry>
    {
        public FailedMessageRetries_ByBatch()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RetryBatchId
                };
        }
    }

    public class RetryBatches_ByStatusAndSession : AbstractIndexCreationTask<RetryBatch>
    {
        public RetryBatches_ByStatusAndSession()
        {
            Map = docs => from doc in docs
                select new
                {
                    doc.RetrySessionId,
                    doc.Status
                };
        }
    }

    public class RetriesGateway
    {
        const int BatchSize = 1000;

        public IDocumentStore Store { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public void StartRetryForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter) where TIndex : AbstractIndexCreationTask, new()
        {
            Task.Factory.StartNew(
                () => CreateAndStageRetriesForIndex<TType, TIndex>(filter, cancellationTokenSource.Token),
                cancellationTokenSource.Token);
        }

        void CreateAndStageRetriesForIndex<TType, TIndex>(Expression<Func<TType, bool>> filter, CancellationToken token) where TIndex : AbstractIndexCreationTask, new()
        {
            using (var session = Store.OpenSession())
            {
                var query = session.Query<TType, TIndex>();

                if (filter != null)
                {
                    query = query.Where(filter);
                }

                var currentBatch = new List<string>();

                using (var stream = session.Advanced.Stream(query.As<FailedMessage>()))
                {
                    while (stream.MoveNext() && !token.IsCancellationRequested)
                    {
                        currentBatch.Add(stream.Current.Document.UniqueMessageId);
                        if (currentBatch.Count == BatchSize)
                        {
                            StageRetryByUniqueMessageIds(currentBatch.ToArray());
                            currentBatch.Clear();
                        }
                    }
                }

                if (currentBatch.Any())
                {
                    StageRetryByUniqueMessageIds(currentBatch.ToArray());
                }
            }
        }

        public void StageRetryByUniqueMessageIds(string[] messageIds)
        {
            if (messageIds == null || !messageIds.Any())
            {
                return;
            }

            var batchDocumentId = RetryDocumentManager.CreateBatchDocument();

            var retryIds = new ConcurrentSet<string>();
            Parallel.ForEach(messageIds, id => retryIds.Add(RetryDocumentManager.CreateFailedMessageRetryDocument(batchDocumentId, id)));

            RetryDocumentManager.MoveBatchToStaging(batchDocumentId, retryIds.ToArray());
        }

        internal void StopProcessingOutstandingBatches()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }

    class RetryStartupAndShutdownTasks : IWantToRunWhenBusStartsAndStops
    {
        public void Start()
        {
            if(RetryDocumentManager != null)
                RetryDocumentManager.AdoptOrphanedBatches();
        }

        public void Stop()
        {
            if(Retries != null)
                Retries.StopProcessingOutstandingBatches();
        }

        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }
    }

    class RetryProcessor : IWantToRunWhenBusStartsAndStops, IDisposable
    {
        static readonly List<string> KeysToRemoveWhenRetryingAMessage = new List<string>
        {
            Headers.Retries,
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace"
        };

        static ILog Log = LogManager.GetLogger(typeof(RetryProcessor));

        public RetryProcessor(IBodyStorage bodyStorage, ISendMessages sender, IDocumentStore store, IBus bus, NoopDequeuer stagingQueueCleaner, ReturnToSenderDequeuer returnToSender)
        {
            executor = new PeriodicExecutor(Process
                , TimeSpan.FromSeconds(30));
            this.bodyStorage = bodyStorage;
            this.sender = sender;
            this.store = store;
            this.bus = bus;
            this.stagingQueueCleaner = stagingQueueCleaner;
            this.returnToSender = returnToSender;
        }

        public void Start()
        {
            executor.Start(false);
        }

        public void Stop()
        {
            executor.Stop(CancellationToken.None);
        }

        public void Dispose()
        {
            Stop();
        }

        void Process()
        {
            bool batchesProcessed;
            do
            {
                using (var session = store.OpenSession())
                {
                    batchesProcessed = ProcessBatches(session);
                    session.SaveChanges();
                }
            } while (batchesProcessed);
        }

        bool ProcessBatches(IDocumentSession session)
        {
            var nowForwarding = session.Include<RetryBatchNowForwarding, RetryBatch>(r => r.RetryBatchId)
                .Load<RetryBatchNowForwarding>(RetryBatchNowForwarding.Id);

            if (nowForwarding != null)
            {
                var forwardingBatch = session.Load<RetryBatch>(nowForwarding.RetryBatchId);

                if (forwardingBatch != null)
                {
                    Forward(forwardingBatch, session);
                }

                session.Delete(nowForwarding);
                return true;
            }

            var stagingBatch = session.Query<RetryBatch>()
                .Customize(q => q.Include<RetryBatch, FailedMessageRetry>(b => b.FailureRetries))
                .FirstOrDefault(b => b.Status == RetryBatchStatus.Staging);

            if (stagingBatch != null)
            {
                Stage(stagingBatch, session);
                session.Store(new RetryBatchNowForwarding { RetryBatchId = stagingBatch.Id }, RetryBatchNowForwarding.Id);
                return true;
            }

            return false;
        }

        void Forward(RetryBatch forwardingBatch, IDocumentSession session)
        {
            returnToSender.Run();

            session.Delete(forwardingBatch);

            Log.InfoFormat("Retry batch {0} done");
        }

        void Stage(RetryBatch stagingBatch, IDocumentSession session)
        {
            stagingQueueCleaner.Run();

            var messageIds = session.Load<FailedMessageRetry>(stagingBatch.FailureRetries)
                    .Where(r => r != null && r.RetryBatchId == stagingBatch.Id)
                    .Select(r => r.FailedMessageId);

            var messages = session.Load<FailedMessage>(messageIds);

            foreach (var message in messages)
            {
                StageMessage(message);
            }

            // TODO: Publish a message on the bus stating that batch has been sent

            stagingBatch.Status = RetryBatchStatus.Forwarding;

            Log.InfoFormat("Retry batch {0} staged {1} messages", stagingBatch.Id, messages.Count());
        }

        void StageMessage(FailedMessage message)
        {
            message.Status = FailedMessageStatus.RetryIssued;

            var attempt = message.ProcessingAttempts.Last();

            var headersToRetryWith = attempt.Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.TargetEndpointAddress"] = attempt.FailureDetails.AddressOfFailingEndpoint;
            headersToRetryWith["ServiceControl.Retry.UniqueMessageId"] = message.UniqueMessageId;

            using (var stream = bodyStorage.Fetch(attempt.MessageId))
            {
                var transportMessage = new TransportMessage(message.Id, headersToRetryWith)
                {
                    Body = ReadFully(stream),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = attempt.MessageIntent
                };

                if (!String.IsNullOrWhiteSpace(attempt.ReplyToAddress))
                {
                    transportMessage.ReplyToAddress = Address.Parse(attempt.ReplyToAddress);
                }

                sender.Send(transportMessage, AdvancedDequeuer.Address);
            }
        }

        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        IBodyStorage bodyStorage;
        ISendMessages sender;
        PeriodicExecutor executor;
        IDocumentStore store;
        IBus bus;
        NoopDequeuer stagingQueueCleaner;
        ReturnToSenderDequeuer returnToSender;
    }

    public class RetryBatchNowForwarding
    {
        public const string Id = "RetryBatches/NowForwarding";
        public string RetryBatchId { get; set; }
    }

    abstract class AdvancedDequeuer : IAdvancedSatellite
    {
        public static Address Address = Address.Parse(Configure.EndpointName).SubScope("staging");
        private DequeueMessagesWrapper receiver;
        private Timer timer;
        ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
        static ILog Log = LogManager.GetLogger(typeof(AdvancedDequeuer));

        protected AdvancedDequeuer()
        {
            timer = new Timer(state => StopInternal());
        }

        protected abstract void HandleMessage(TransportMessage message);

        public bool Handle(TransportMessage message)
        {
            HandleMessage(message);
            timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
            return true;
        }

        public void Start()
        {
        }

        public void Run()
        {
            try
            {
                resetEvent.Reset();
                receiver.StartInternal();
                timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                Log.InfoFormat("{0} started", GetType().Name);
            }
            finally
            {
                resetEvent.Wait();
            }
        }

        public void Stop()
        {
            StopInternal();
        }

        void StopInternal()
        {
            receiver.StopInternal();
            resetEvent.Set();
            Log.InfoFormat("{0} stopped", GetType().Name);
        }

        public Address InputAddress { get { return Address; } }
        public bool Disabled { get { return false; } }
        public Action<TransportReceiver> GetReceiverCustomization()
        {
            return r =>
            {
                receiver = new DequeueMessagesWrapper(r.Receiver);
                r.Receiver = receiver;
            };
        }

        internal class DequeueMessagesWrapper : IDequeueMessages
        {
            private readonly IDequeueMessages _realDequeuer;
            private int maximumConcurrencyLevel;
            private int disposeSignaled;

            public DequeueMessagesWrapper(IDequeueMessages realDequeuer)
            {
                _realDequeuer = realDequeuer;
            }

            public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
            {
                _realDequeuer.Init(address, transactionSettings, tryProcessMessage, endProcessMessage);
            }

            public void StartInternal()
            {
                Interlocked.Exchange(ref disposeSignaled, 0);
                _realDequeuer.Start(maximumConcurrencyLevel);
            }

            public void Start(int maximumConcurrencyLevel)
            {
                this.maximumConcurrencyLevel = maximumConcurrencyLevel;
            }

            public void Stop()
            {
            }

            public void StopInternal()
            {
                if (Interlocked.Exchange(ref disposeSignaled, 1) != 0)
                {
                    return;
                }
                _realDequeuer.Stop();
            }
        }
    }

    class NoopDequeuer : AdvancedDequeuer
    {
        protected override void HandleMessage(TransportMessage message)
        {
        }
    }

    class ReturnToSenderDequeuer : AdvancedDequeuer
    {
        readonly ISendMessages sender;

        public ReturnToSenderDequeuer(ISendMessages sender)
        {
            this.sender = sender;
        }

        protected override void HandleMessage(TransportMessage message)
        {
            var destinationAddress = Address.Parse(message.Headers["ServiceControl.TargetEndpointAddress"]);

            message.Headers.Remove("ServiceControl.TargetEndpointAddress");

            sender.Send(message, destinationAddress);
        }
    }
}
