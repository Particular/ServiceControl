namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using Contracts.Operations;
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

    class MsmqAuditQueueImporter //: IWantToRunWhenBusStartsAndStops
    {
        public MsmqAuditQueueImporter(IDocumentStore store, IBuilder builder, IDequeueMessages receiver, CriticalError criticalError)
        {
            this.store = store;
            this.builder = builder;
            enabled = receiver is MsmqDequeueStrategy;
            CriticalError = criticalError;

            importFailuresHandler = new SatelliteImportFailuresHandler(store,
                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm,
                }, CriticalError);
        }

        public UnicastBus UnicastBus { get; set; }
        public ISendMessages Forwarder { get; set; }
        CriticalError CriticalError;

        public void Start()
        {
            // Any messages that fail conversion to a transportmessage is sent to the particular.servicecontrol.errors queue using low level Api
            // The actual queue name is based on service name to support mulitple instances on same host (particular.servicecontrol.errors is the default)
            var serviceControlErrorQueueAddress = Address.Parse(string.Format("{0}.errors", Settings.ServiceName));
            serviceControlErrorQueue = new MessageQueue(MsmqUtilities.GetFullPath(serviceControlErrorQueueAddress), false, true, QueueAccessMode.Send);

            if (!enabled)
            {
                return;
            }

            if (Settings.AuditQueue == Address.Undefined)
            {
                Logger.Info("No Audit queue has been configured. No audit import will be performed. To enable imports add the ServiceBus/AuditQueue appsetting and restart ServiceControl");
                return;
            }

            if (TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
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

        bool TerminateIfForwardingIsEnabledButQueueNotWritable()
        {
            if (Settings.ForwardAuditMessages != true)
            {
                return false;
            }

            try
            {
                //Send a message to test the forwarding queue
                var testMessage = new TransportMessage(Guid.Empty.ToString("N"), new Dictionary<string, string>());
                Forwarder.Send(testMessage, new SendOptions(Settings.AuditLogQueue));
                return false;
            }
            catch (Exception messageForwardingException)
            {
                //This call to RaiseCriticalError has to be on a seperate thread  otherwise it deadlocks and doesn't stop correctly.  
                ThreadPool.QueueUserWorkItem(state => CriticalError.Raise("Audit Import cannot start", messageForwardingException));
                return true;
            }
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

            // If batchErrorLockObj can not be locked it means one of the Tasks has had a batch error, and RetryMessageImportById is running
            
            lock (batchErrorLockObj)
            {
            }

            if (stopping)
                return true;
            
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
                Logger.DebugFormat("Batch job started", Task.CurrentId);
                
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
                            //using( var bulkInsert = store.BulkInsert( options: new BulkInsertOptions { CheckForUpdates = true } ) )
                            using( var bulkInsert = store.BulkInsert( options: new BulkInsertOptions() { SkipOverwriteIfUnchanged = true } ) )
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

                                        if (Settings.ForwardAuditMessages == true)
                                        {
                                            Forwarder.Send(transportMessage, new SendOptions(Settings.AuditLogQueue));
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
            // Try to get the batchErrorLock, if we can't then exit, 
            // the message will trigger a retry next time on the next batch read.
            // Retrymessage may be fired again for the same message until the batches drain so this 
            // prevents the message being processed twice, 
            if (Monitor.TryEnter(batchErrorLockObj))
            {
                try
                {
                    Logger.DebugFormat("Drain stop running batch importers");
                    stopping = true;
                    var runningTasks = batchTaskTracker.Active();
                    Task.WaitAll(runningTasks);

                    var commitTransaction = false;
                    using (var queueReceiver = CreateReceiver())
                    using (var msmqTransaction = new MessageQueueTransaction())
                    {
                        msmqTransaction.Begin();
                        Logger.DebugFormat("Retry import of messageID - {0}", messageID);
                        try
                        {
                            Message message;
                            TransportMessage transportMessage;
                            try
                            {
                                message = queueReceiver.ReceiveById(messageID);
                                performanceCounters.MessageDequeued();
                            }
                            catch (Exception exception)
                            {
                                importFailuresHandler.FailedToReceive(exception); //logs and increments circuit breaker
                                return;
                            }

                            try
                            {
                                transportMessage = MsmqUtilities.Convert(message);
                            }
                            catch (Exception convertException)
                            {
                                importFailuresHandler.FailedToReceive(convertException); //logs and increments circuit breaker
                                serviceControlErrorQueue.Send(message, msmqTransaction); // Send unconvertable message to SC's ErrorQueue so it's not lost
                                commitTransaction = true; // Can't convert the messsage, so commit to get message out of the queue
                                return;
                            }

                            try
                            {
                                var importSuccessfullyProcessedMessage = new ImportSuccessfullyProcessedMessage(transportMessage);
                                foreach (var enricher in enrichers)
                                {
                                    enricher.Enrich(importSuccessfullyProcessedMessage);
                                }

                                using (var session = store.OpenSession())
                                {
                                    var auditMessage = new ProcessedMessage(importSuccessfullyProcessedMessage);
                                    session.Store(auditMessage);
                                    session.SaveChanges();
                                }
                                performanceCounters.MessageProcessed();

                                if (Settings.ForwardAuditMessages == true)
                                {
                                    Forwarder.Send(transportMessage, new SendOptions(Settings.AuditLogQueue));
                                }

                                commitTransaction = true;
                            }
                            catch (Exception importException)
                            {
                                importFailuresHandler.Log(transportMessage, importException); //Logs and Writes failure transport message to Raven
                            }
                        }
                        finally
                        {
                            if (commitTransaction)  
                            {
                                msmqTransaction.Commit();
                            }
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(batchErrorLockObj);
                    //Restart Batch mode
                    stopping = false;
                    Logger.Debug("Ready to BeginPeek again");
                    queuePeeker.BeginPeek();
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
        MessageQueue serviceControlErrorQueue;

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

    /// <summary>
    ///     MSMQ-related utility functions
    /// </summary>
    public class MsmqUtilities
    {
        /// <summary>
        /// Returns the full path without Format or direct os
        ///             from an address.
        /// 
        /// </summary>
        public static string GetFullPathWithoutPrefix(Address address)
        {
            return address.Machine + "\\private$\\" + address.Queue;
        }

        /// <summary>
        ///     Turns a '@' separated value into a full path.
        ///     Format is 'queue@machine', or 'queue@ipaddress'
        /// </summary>
        public static string GetFullPath(Address value)
        {
            IPAddress ipAddress;
            if (IPAddress.TryParse(value.Machine, out ipAddress))
            {
                return PREFIX_TCP + GetFullPathWithoutPrefix(value);
            }

            return PREFIX + GetFullPathWithoutPrefix(value);
        }

        /// <summary>
        ///     Gets the name of the return address from the provided value.
        ///     If the target includes a machine name, uses the local machine name in the returned value
        ///     otherwise uses the local IP address in the returned value.
        /// </summary>
        public static string GetReturnAddress(string value, string target)
        {
            return GetReturnAddress(Address.Parse(value), Address.Parse(target));
        }

        /// <summary>
        ///     Gets the name of the return address from the provided value.
        ///     If the target includes a machine name, uses the local machine name in the returned value
        ///     otherwise uses the local IP address in the returned value.
        /// </summary>
        public static string GetReturnAddress(Address value, Address target)
        {
            var machine = target.Machine;

            IPAddress targetIpAddress;

            //see if the target is an IP address, if so, get our own local ip address
            if (IPAddress.TryParse(machine, out targetIpAddress))
            {
                if (string.IsNullOrEmpty(localIp))
                {
                    localIp = LocalIpAddress(targetIpAddress);
                }

                return PREFIX_TCP + localIp + PRIVATE + value.Queue;
            }

            return PREFIX + GetFullPathWithoutPrefix(value);
        }

        static string LocalIpAddress(IPAddress targetIpAddress)
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            var availableAddresses =
                networkInterfaces.Where(
                    ni =>
                        ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses).ToList();

            var firstWithMatchingFamily =
                availableAddresses.FirstOrDefault(a => a.Address.AddressFamily == targetIpAddress.AddressFamily);

            if (firstWithMatchingFamily != null)
            {
                return firstWithMatchingFamily.Address.ToString();
            }

            var fallbackToDifferentFamily = availableAddresses.FirstOrDefault();

            if (fallbackToDifferentFamily != null)
            {
                return fallbackToDifferentFamily.Address.ToString();
            }

            return "127.0.0.1";
        }


        static Address GetIndependentAddressForQueue(MessageQueue q)
        {
            if (q == null)
            {
                return null;
            }

            var arr = q.FormatName.Split('\\');
            var queueName = arr[arr.Length - 1];

            var directPrefixIndex = arr[0].IndexOf(DIRECTPREFIX);
            if (directPrefixIndex >= 0)
            {
                return new Address(queueName, arr[0].Substring(directPrefixIndex + DIRECTPREFIX.Length));
            }

            var tcpPrefixIndex = arr[0].IndexOf(DIRECTPREFIX_TCP);
            if (tcpPrefixIndex >= 0)
            {
                return new Address(queueName, arr[0].Substring(tcpPrefixIndex + DIRECTPREFIX_TCP.Length));
            }

            try
            {
                // the pessimistic approach failed, try the optimistic approach
                arr = q.QueueName.Split('\\');
                queueName = arr[arr.Length - 1];
                return new Address(queueName, q.MachineName);
            }
            catch
            {
                throw new Exception("Could not translate format name to independent name: " + q.FormatName);
            }
        }

        /// <summary>
        ///     Converts an MSMQ message to a TransportMessage.
        /// </summary>
        public static TransportMessage Convert(Message m)
        {
            var headers = DeserializeMessageHeaders(m);


            var result = new TransportMessage(m.Id, headers)
            {
                Recoverable = m.Recoverable,
                TimeToBeReceived = m.TimeToBeReceived,
                CorrelationId = GetCorrelationId(m, headers)
            };

            //note: we can drop this line when we no longer support interop btw v3 + v4
            if (m.ResponseQueue != null)
            {
                result.Headers[Headers.ReplyToAddress] = GetIndependentAddressForQueue(m.ResponseQueue).ToString();
            }


            if (Enum.IsDefined(typeof(MessageIntentEnum), m.AppSpecific))
            {
                result.MessageIntent = (MessageIntentEnum)m.AppSpecific;
            }

            m.BodyStream.Position = 0;
            result.Body = new byte[m.BodyStream.Length];
            m.BodyStream.Read(result.Body, 0, result.Body.Length);

            return result;
        }

        static string GetCorrelationId(Message message, Dictionary<string, string> headers)
        {
            string correlationId;

            if (headers.TryGetValue(Headers.CorrelationId, out correlationId))
            {
                return correlationId;
            }

            if (message.CorrelationId == "00000000-0000-0000-0000-000000000000\\0")
            {
                return null;
            }

            //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end that the sender added to make it compatible
            //The replace can be removed in v5 since only v3 messages will need this
            return message.CorrelationId.Replace("\\0", "");
        }

        static Dictionary<string, string> DeserializeMessageHeaders(Message m)
        {
            var result = new Dictionary<string, string>();

            if (m.Extension.Length == 0)
            {
                return result;
            }

            //This is to make us compatible with v3 messages that are affected by this bug:
            //http://stackoverflow.com/questions/3779690/xml-serialization-appending-the-0-backslash-0-or-null-character
            var extension = Encoding.UTF8.GetString(m.Extension).TrimEnd('\0');
            object o;
            using (var stream = new StringReader(extension))
            {
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    CheckCharacters = false
                }))
                {
                    o = headerSerializer.Deserialize(reader);
                }
            }

            foreach (var pair in (List<HeaderInfo>)o)
            {
                if (pair.Key != null)
                {
                    result.Add(pair.Key, pair.Value);
                }
            }

            return result;
        }

        /// <summary>
        ///     Converts a TransportMessage to an Msmq message.
        ///     Doesn't set the ResponseQueue of the result.
        /// </summary>
        public static Message Convert(TransportMessage message)
        {
            var result = new Message();

            if (message.Body != null)
            {
                result.BodyStream = new MemoryStream(message.Body);
            }


            AssignMsmqNativeCorrelationId(message, result);

            result.Recoverable = message.Recoverable;

            if (message.TimeToBeReceived < MessageQueue.InfiniteTimeout)
            {
                result.TimeToBeReceived = message.TimeToBeReceived;
            }

            using (var stream = new MemoryStream())
            {
                headerSerializer.Serialize(stream, message.Headers.Select(pair => new HeaderInfo
                {
                    Key = pair.Key,
                    Value = pair.Value
                }).ToList());
                result.Extension = stream.ToArray();
            }

            result.AppSpecific = (int)message.MessageIntent;

            return result;
        }

        static void AssignMsmqNativeCorrelationId(TransportMessage message, Message result)
        {
            if (string.IsNullOrEmpty(message.CorrelationId))
            {
                return;
            }

            Guid correlationId;

            if (Guid.TryParse(message.CorrelationId, out correlationId))
            {
                //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end to make it compatible                
                result.CorrelationId = message.CorrelationId + "\\0";
                return;
            }

            try
            {
                if (message.CorrelationId.Contains("\\"))
                {
                    var parts = message.CorrelationId.Split('\\');

                    int number;

                    if (parts.Count() == 2 && Guid.TryParse(parts.First(), out correlationId) &&
                        int.TryParse(parts[1], out number))
                    {
                        result.CorrelationId = message.CorrelationId;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to assign a native correlation id for message: " + message.Id, ex);
            }
        }

        const string DIRECTPREFIX = "DIRECT=OS:";
        const string DIRECTPREFIX_TCP = "DIRECT=TCP:";
        const string PREFIX_TCP = "FormatName:" + DIRECTPREFIX_TCP;
        const string PREFIX = "FormatName:" + DIRECTPREFIX;
        internal const string PRIVATE = "\\private$\\";
        static string localIp;
        static System.Xml.Serialization.XmlSerializer headerSerializer = new System.Xml.Serialization.XmlSerializer(typeof(List<HeaderInfo>));
        static ILog Logger = LogManager.GetLogger<MsmqUtilities>();
    }
}