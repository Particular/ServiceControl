namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using MessageFailed = ServiceControl.Contracts.MessageFailed;

    public class EventDispatcher : IWantToRunWhenBusStartsAndStops
    {
        const int BatchSize = 100;
        public IDocumentStore DocumentStore { get; set; }
        public IBus Bus { get; set; }

        public void Start()
        {
            tokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => DispatchEvents(tokenSource.Token));
        }

        public void Stop()
        {
            tokenSource.Cancel();
        }

        private void DispatchEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                DispatchEventBatch(token);
            }
        }

        void DispatchEventBatch(CancellationToken token)
        {
            using (var session = DocumentStore.OpenSession())
            {
                var awaitingDispatching = session.Query<MessageFailedDispatchRequest>().Take(BatchSize).ToList();
                if (!awaitingDispatching.Any())
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Nothing to dispatch. Waiting...");
                    }
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    return;
                }


                var failedMessageIds = awaitingDispatching
                    .Select(x => FailedMessage.MakeDocumentId(x.FailedMessageId))
                    .ToArray();

                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Dispatching {0} events.",failedMessageIds.Length);
                }
                var failedMessageData = session.Load<FailedMessage>(failedMessageIds);

                foreach (var messageFailed in failedMessageData)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Publishing external event on the bus.");
                    }
                    Bus.Publish(ConvertToEvent(messageFailed));
                }

                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    session.Delete(dispatchedEvent);
                }
                session.SaveChanges();
            }
        }

        private static MessageFailed ConvertToEvent(FailedMessage message)
        {
            var last = message.ProcessingAttempts.Last();
            var sendingEndpoint = (Contracts.Operations.EndpointDetails) last.MessageMetadata["SendingEndpoint"];
            var receivingEndpoint = (Contracts.Operations.EndpointDetails) last.MessageMetadata["ReceivingEndpoint"];
            return new MessageFailed()
            {
                FailedMessageId = message.UniqueMessageId,
                MessageType = (string) last.MessageMetadata["MessageType"],
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status == FailedMessageStatus.Archived
                    ? MessageFailed.MessageStatus.ArchivedFailure
                    : message.ProcessingAttempts.Count == 1
                        ? MessageFailed.MessageStatus.Failed
                        : MessageFailed.MessageStatus.RepeatedFailure,
                ProcessingDetails = new MessageFailed.ProcessingInfo
                {
                    SendingEndpoint = new MessageFailed.ProcessingInfo.Endpoint()
                    {
                        Host = sendingEndpoint.Host,
                        HostId = sendingEndpoint.HostId,
                        Name = sendingEndpoint.Name
                    },
                    ProcessingEndpoint = new MessageFailed.ProcessingInfo.Endpoint()
                    {
                        Host = receivingEndpoint.Host,
                        HostId = receivingEndpoint.HostId,
                        Name = receivingEndpoint.Name
                    },
                },
                MessageDetails = new MessageFailed.Message()
                {
                    Headers = last.Headers,
                    ContentType = (string) last.MessageMetadata["ContentType"],
                    Body = (string) last.MessageMetadata["Body"],
                    MessageId = last.MessageId,
                },
                FailureDetails = new MessageFailed.FailureInfo
                {
                    AddressOfFailingEndpoint = last.FailureDetails.AddressOfFailingEndpoint,
                    TimeOfFailure = last.FailureDetails.TimeOfFailure,
                    Exception = new MessageFailed.FailureInfo.ExceptionInfo
                    {
                        ExceptionType = last.FailureDetails.Exception.ExceptionType,
                        Message = last.FailureDetails.Exception.Message,
                        Source = last.FailureDetails.Exception.Source,
                        StackTrace = last.FailureDetails.Exception.StackTrace,
                    },
                },
            };
        }

        CancellationTokenSource tokenSource;

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
    }
}