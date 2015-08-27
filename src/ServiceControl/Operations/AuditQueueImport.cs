namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Contracts.Operations;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Messages;
    using NServiceBus.Unicast.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.RavenDB;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;

    public class AuditQueueImport : IAdvancedSatellite, IDisposable
    {
        static readonly Metrics.Timer auditQueueImportTimer = Metric.Timer( "AuditQueueImport time", Unit.Requests );
        static readonly Metrics.Timer invokePipelineTimer = Metric.Timer( "InvokePipeline time", Unit.Requests );
        static readonly Metrics.Timer enricherTimer = Metric.Timer( "Enricher time", Unit.Requests );

        public IBuilder Builder { get; set; }
        public ISendMessages Forwarder { get; set; }
        public PipelineExecutor PipelineExecutor { get; set; }
        public LogicalMessageFactory LogicalMessageFactory { get; set; }
        public CriticalError CriticalError { get; set; }

        //public BusNotifications BusNotifications { get; set; }

        //private IDisposable receiveStartedUnsubscribe;

        public AuditQueueImport(IDequeueMessages receiver)
        {
            disabled = false;
        }

        //bool piplelineInstrumented = false;
        //int sessionIdCounter;

        //void OnReceiveStarted( IObservable<StepStarted> stepStartedStream )
        //{
        //    new Run( Interlocked.Increment( ref sessionIdCounter ), stepStartedStream );
        //}

        //class Run
        //{
        //    //private int numberOfSpacesToIndent = 1;

        //    public Run( int sessionId, IObservable<StepStarted> stepStartedStream )
        //    {
        //        //Console.Out.WriteLine( "{0} Run Started", sessionId );

        //        stepStartedStream.Subscribe( started => new Step( started, sessionId ), () => Console.Out.WriteLine( "{0} Run Ended", sessionId ) );
        //    }
        //}

        //class Step
        //{
        //    private Type behavior;
        //    private string stepId;
        //    private TimeSpan duration = TimeSpan.Zero;
        //    //private Exception exception;
        //    private int sessionId;
        //    //private string spaces;
        //    TimerContext timerContext;

        //    public Step( StepStarted started, int id )
        //    {
        //        behavior = started.Behavior;
        //        stepId = started.StepId;
        //        sessionId = id;
        //        timerContext = pipelineStepTimer.NewContext(stepId);
        //        //spaces = new string( ' ', numberOfSpacesToIndent );

        //        //Console.WriteLine( Header() );

        //        started.Ended.Subscribe( ended => duration = ended.Duration,
        //            ex =>
        //            {
        //                //exception = ex;
        //                //Console.WriteLine( Footer() );
        //                timerContext.Dispose();
        //            }
        //            , () =>
        //            {
        //                //Console.WriteLine( Footer() )
        //                timerContext.Dispose();
        //            } );
        //    }

        //    //string Header()
        //    //{
        //    //    return String.Format( "{3}{0}) [{1}] {2}", sessionId, stepId, behavior.FullName, spaces );
        //    //}

        //    //string Footer()
        //    //{
        //    //    if( exception == null )
        //    //    {
        //    //        return String.Format( "{3}{0}) [{1}] {2:g}ms", sessionId, stepId, duration, spaces );
        //    //    }
        //    //    return String.Format( "{3}{0})[{1}] {2}", sessionId, stepId, exception.GetType(), spaces );
        //    //}
        //}

        public bool Handle(TransportMessage message)
        {
            //if (!piplelineInstrumented)
            //{
            //    receiveStartedUnsubscribe = BusNotifications.Pipeline.ReceiveStarted
            //       .SubscribeOn( Scheduler.Default )
            //       .Subscribe( OnReceiveStarted,
            //           ex =>
            //           {
            //               Console.WriteLine( "An error occurred: {0}", ex );
            //           },
            //           () => Console.WriteLine( "OnReceiveEnded" ) );
       
            //    piplelineInstrumented = true;
            //}

            using (auditQueueImportTimer.NewContext(message.Id))
            {
                InnerHandle( message );   
            }

            return true;
        }

        void InnerHandle(TransportMessage message)
        {
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);
            
            using( var childBuilder = Builder.CreateChildBuilder() )
            {
                PipelineExecutor.CurrentContext.Set(childBuilder);

                foreach( var enricher in childBuilder.BuildAll<IEnrichImportedMessages>() )
                {
                    using (enricherTimer.NewContext(enricher.GetType().Name))
                    {
                        enricher.Enrich( receivedMessage );
                    }
                }

                var logicalMessage = LogicalMessageFactory.Create( receivedMessage );

                var context = new IncomingContext(PipelineExecutor.CurrentContext, message)
                {
                    LogicalMessages = new List<LogicalMessage>
                    {
                        logicalMessage
                    },
                    IncomingLogicalMessage = logicalMessage
                };

                context.Set("NServiceBus.CallbackInvocationBehavior.CallbackWasInvoked", false);

                var behaviors = behavioursToAddFirst.Concat(PipelineExecutor.Incoming.SkipWhile(r => r.StepId != WellKnownStep.LoadHandlers).Select(r => r.BehaviorType));

                using (invokePipelineTimer.NewContext())
                {
                    PipelineExecutor.InvokePipeline(behaviors, context);
                }
            }

            if (Settings.ForwardAuditMessages == true)
            {
                Forwarder.Send(message, new SendOptions(Settings.AuditLogQueue));
            }
        }

        Type[] behavioursToAddFirst = new[] { typeof(RavenUnitOfWorkBehavior) };

        public void Start()
        {
            if (!TerminateIfForwardingIsEnabledButQueueNotWritable())
            {
                Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);    
            }
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
       

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.AuditQueue; }
        }

        public bool Disabled
        {
            get { return disabled; }
        }

        public Action<TransportReceiver> GetReceiverCustomization()
        {
            satelliteImportFailuresHandler = new SatelliteImportFailuresHandler(Builder.Build<IDocumentStore>(),
                Path.Combine(Settings.LogPath, @"FailedImports\Audit"), tm => new FailedAuditImport
                {
                    Message = tm,
                }, 
                CriticalError);

            return receiver => { receiver.FailureManager = satelliteImportFailuresHandler; };
        }

        public void Dispose()
        {
            if (satelliteImportFailuresHandler != null)
            {
                satelliteImportFailuresHandler.Dispose();
            }
        }

        SatelliteImportFailuresHandler satelliteImportFailuresHandler;

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
        bool disabled;
    }
}