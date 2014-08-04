namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using EndpointControl.Handlers;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transports;
    using NServiceBus.Transports.Msmq;
    using NServiceBus.Unicast;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    class MsmqAuditQueueImporter : IWantToRunWhenBusStartsAndStops
    {
        public MsmqAuditQueueImporter(IDocumentStore store, IBuilder builder, IDequeueMessages receiver)
        {
            this.store = store;
            this.builder = builder;
            enabled = receiver is MsmqDequeueStrategy;

            importFailuresHandler = new SatelliteImportFailuresHandler(store,
                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm,
                });
        }

        public IBus Bus { get; set; }
        public KnownEndpointsCache KnownEndpointsCache { get; set; }
        public UnicastBus UnicastBus { get; set; }
        public ISendMessages Forwarder { get; set; }

        public void Start()
        {
            if (!enabled)
            {
                return;
            }

            if (Settings.AuditQueue == Address.Undefined)
            {
                Logger.Info("No Audit queue has been configured. No audit import will be performed. To enable imports add the ServiceBus/AuditQueue appsetting and restart ServiceControl");
                return;
            }

            performanceCounters.Initialize();

            queuePeeker = new MessageQueue(MsmqUtilities.GetFullPath(Settings.AuditQueue), QueueAccessMode.Peek);
            queuePeeker.MessageReadPropertyFilter.ClearAll();
            queuePeeker.PeekCompleted += QueueOnPeekCompleted;

            enrichers = builder.BuildAll<IEnrichImportedMessages>().ToList();

            Logger.InfoFormat("MSMQ Audit import is now started, feeding audit messages from: {0}", Settings.AuditQueue);

            countDownEvent.Idle += OnIdle;

            Logger.Debug("Ready to BeginPeek");
            queuePeeker.BeginPeek();
        }

        public void Stop()
        {
            if (!enabled)
            {
                return;
            }

            stopping = true;

            queuePeeker.PeekCompleted -= QueueOnPeekCompleted;

            stopResetEvent.Wait();

            performanceCounters.Dispose();

            queuePeeker.Dispose();

            stopResetEvent.Dispose();
        }

        static MessageQueue CreateReceiver()
        {
            var queue = new MessageQueue(MsmqUtilities.GetFullPath(Settings.AuditQueue), QueueAccessMode.Receive);

            var messageReadPropertyFilter = new MessagePropertyFilter
            {
                Body = true,
                TimeToBeReceived = true,
                Recoverable = true,
                Id = true,
                ResponseQueue = true,
                CorrelationId = true,
                Extension = true,
                AppSpecific = true
            };

            queue.MessageReadPropertyFilter = messageReadPropertyFilter;

            return queue;
        }

        void OnIdle(object sender, EventArgs eventArgs)
        {
            stopResetEvent.Set();

            if (stopping)
            {
                return;
            }

            Logger.Debug("Ready to BeginPeek again");
            queuePeeker.BeginPeek();
        }

        void QueueOnPeekCompleted(object sender, PeekCompletedEventArgs args)
        {
            stopResetEvent.Reset();

            TryStartNewBatchImporter();
        }

        bool TryStartNewBatchImporter()
        {
            lock (lockObj)
            {
                if (countDownEvent.CurrentCount > UnicastBus.Transport.MaximumConcurrencyLevel)
                {
                    return false;
                }
                countDownEvent.Add();
            }

            // if batchErrorLockObj can not be locked it means one of the Tasks has had a batch error, and that Task is 
            // going to try non-batch mode for member of the batch   while this is happening new batchimporters are prevented 
            // from starting to ensure the message isn't read into that batch
             
            if (Monitor.TryEnter(batchErrorLockObj, TimeSpan.FromSeconds(10)))
            {
                Monitor.Exit(batchErrorLockObj);
            }
            
            batchTaskTracker.Add(Task.Factory
                .StartNew(BatchImporter, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(task =>
                {
                    if (task.Exception != null) { 
                        task.Exception.Handle(ex =>{
                            Logger.Error("Error processing message.", ex);
                            return true;
                        });
                        batchTaskTracker.Remove(task);
                    }
                }));
            return true;
        }

        void BatchImporter()
        {
            String failedMessageID = null;

            try
            { 
                Logger.Debug("Batch job started");
                var moreMessages = 0;

                using (var queueReceiver = CreateReceiver())
                {
                    do
                    {
                        if (moreMessages > RampUpConcurrencyMagicNumber)
                        {
                            if (TryStartNewBatchImporter())
                            {
                                Logger.Debug("We have too many messages, starting another batch importer");
                                moreMessages = 0; //Reset to 0 so we only ramp up once per BatchImporter
                            }
                        }

                        moreMessages++;

                        using (var msmqTransaction = new MessageQueueTransaction())
                        {
                            msmqTransaction.Begin();
                            using (var bulkInsert =store.BulkInsert(options:new BulkInsertOptions {CheckForUpdates = true}))
                            {
                                for (var idx = 0; idx < BatchSize; idx++)
                                {
                                    Message message = null;
                                    TransportMessage transportMessage;
                                    try
                                    {
                                        message = queueReceiver.Receive(receiveTimeout, msmqTransaction);
                                        performanceCounters.MessageDequeued();
                                        transportMessage = MsmqUtilities.Convert(message);
                                    }
                                    catch (MessageQueueException mqe)
                                    {
                                        if (mqe.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                                        {
                                            moreMessages = 0;
                                            break;
                                        }
                                        throw;
                                    }
                                    catch (Exception)
                                    {
                                        if (message != null) {
                                            failedMessageID = message.Id;
                                        }
                                        throw;
                                    }
                                                
                                    try
                                    {
                                        var importSuccessfullyProcessedMessage = new ImportSuccessfullyProcessedMessage(transportMessage);
                                        foreach (var enricher in enrichers)
                                        {
                                            enricher.Enrich(importSuccessfullyProcessedMessage);
                                        }
                                        var auditMessage = new ProcessedMessage(importSuccessfullyProcessedMessage);
                                        bulkInsert.Store(auditMessage);
                                        performanceCounters.MessageProcessed();

                                        if (Settings.ForwardAuditMessages)
                                        {
                                            Forwarder.Send(transportMessage, Settings.AuditLogQueue);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        if (message != null)
                                        {
                                            failedMessageID = message.Id;
                                        }
                                        throw;
                                    }
                                }
                            }

                            msmqTransaction.Commit();
                        }
                    } while (moreMessages > 0 && !stopping);
                }
                Logger.Debug("Stopping batch importer");
            }
            finally
            {
                if (!String.IsNullOrEmpty(failedMessageID))
                {
                    // Call RetryMessageImportById outside the Task as it checks for running tasks
                    ThreadPool.QueueUserWorkItem(state => RetryMessageImportById(failedMessageID));
                }
                countDownEvent.Decrement();
            }
        }

        void RetryMessageImportById(string messageID)
        {
            //If we can't get a lock then skip, the message will still be in the queue and will retrigger this when it fails on a subsequent batch run
            if (Monitor.TryEnter(batchErrorLockObj, receiveTimeout))
            {
                try
                {
                    //Wait for all the current batchmporters to complete - because we've locked batchErrorLockObj then no batch imports will run in parallel
                    var runningTasks = batchTaskTracker.Active();
                    Task.WaitAll(runningTasks);
                    TransportMessage transportMessage;
                    try
                    {
                        using (var queueReceiver = CreateReceiver())
                        {
                            using (var msmqTransaction = new MessageQueueTransaction())
                            {
                                msmqTransaction.Begin();
                                var message = queueReceiver.ReceiveById(messageID);
                                performanceCounters.MessageDequeued();
                                transportMessage = MsmqUtilities.Convert(message);
                                msmqTransaction.Commit();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        importFailuresHandler.FailedToReceive(ex);
                        return;
                    }

                    try
                    {
                        var importSuccessfullyProcessedMessage = new ImportSuccessfullyProcessedMessage(transportMessage);
                        foreach (var enricher in enrichers)
                        {
                            enricher.Enrich(importSuccessfullyProcessedMessage);
                        }
                        using (var bulkInsert = store.BulkInsert(options: new BulkInsertOptions{CheckForUpdates = true}))
                        {
                            var auditMessage = new ProcessedMessage(importSuccessfullyProcessedMessage);
                            bulkInsert.Store(auditMessage);
                        }
                        performanceCounters.MessageProcessed();
                        if (Settings.ForwardAuditMessages)
                        {
                            Forwarder.Send(transportMessage, Settings.AuditLogQueue);
                        }
                    }
                    catch (Exception ex)
                    {
                        importFailuresHandler.Log(transportMessage, ex);
                    }
                }
                finally
                {
                    Monitor.Exit(batchErrorLockObj);
                }
            }
        }

        const int RampUpConcurrencyMagicNumber = 5; //How many batches before we ramp up?
        const int BatchSize = 100;  

        static readonly ILog Logger = LogManager.GetLogger(typeof(MsmqAuditQueueImporter));

        readonly IBuilder builder;
        readonly CountDownEvent countDownEvent = new CountDownEvent();
        readonly bool enabled;
        readonly SatelliteImportFailuresHandler importFailuresHandler;
        readonly object lockObj = new object();
        readonly object batchErrorLockObj = new object();
        readonly MsmqAuditImporterPerformanceCounters performanceCounters = new MsmqAuditImporterPerformanceCounters();
        readonly TimeSpan receiveTimeout = TimeSpan.FromSeconds(1);
        readonly ManualResetEventSlim stopResetEvent = new ManualResetEventSlim(true);
        readonly IDocumentStore store;

        BatchTaskTracker batchTaskTracker = new BatchTaskTracker();
        List<IEnrichImportedMessages> enrichers;
        MessageQueue queuePeeker;
        volatile bool stopping;

        class BatchTaskTracker
        {
            List<Task> tasks = new List<Task>();
            
            public void Add(Task task)
            {
                lock (tasks)
                {
                    tasks.Add(task);
                }
            }

            public void Remove(Task task)
            {
                lock (tasks)
                {
                    tasks.Remove(task);
                }
            }

            public Task[] Active()
            {
                lock (tasks)
                {
                    return tasks.Where(x => !x.IsCompleted).ToArray();
                }
            }
        }

        class CountDownEvent
        {
            public int CurrentCount
            {
                get { return counter; }
            }

            public event EventHandler Idle;

            public void Add()
            {
#pragma warning disable 420
                Interlocked.Increment(ref counter);
#pragma warning restore 420
            }

            public void Decrement()
            {
#pragma warning disable 420
                if (Interlocked.Decrement(ref counter) == 0)
#pragma warning restore 420
                {
                    Idle(this, EventArgs.Empty);
                }
            }

            volatile int counter;
        }
    }
}