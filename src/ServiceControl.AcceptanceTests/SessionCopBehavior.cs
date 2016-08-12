namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Settings;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;

    internal class RegisterWrappers : IWantToRunWhenConfigurationIsComplete
    {
        public RegisterWrappers(ISendMessages sender, IPublishMessages publisher, IDeferMessages defer, ScenarioContext sc, ReadOnlySettings settings)
        {
            var container = settings.Get<IConfigureComponents>("SC.ConfigureComponent");
            container.RegisterSingleton(typeof(ISendMessages), new SendMessagesWrapper(sender, sc));
            container.RegisterSingleton(typeof(IPublishMessages), new PublishMessagesWrapper(publisher, sc));
            container.RegisterSingleton(typeof(IDeferMessages), new DeferMessagesWrapper(defer, sc));
        }

        public void Run(Configure config)
        {
            // We need to do the override on the ctor otherwise is too late!
        }
    }

    internal class PublishMessagesWrapper : IPublishMessages
    {
        private string sessionId;
        private IPublishMessages wrappedPublisher;

        public PublishMessagesWrapper(IPublishMessages wrappedPublisher, ScenarioContext context)
        {
            this.wrappedPublisher = wrappedPublisher;
            sessionId = context.SessionId;
        }

        public void Publish(TransportMessage message, PublishOptions publishOptions)
        {
            message.Headers[SessionCopInBehavior.Header] = sessionId;
            wrappedPublisher.Publish(message, publishOptions);
        }
    }

    internal class SendMessagesWrapper : ISendMessages
    {
        private string sessionId;

        private ISendMessages wrappedSender;

        public SendMessagesWrapper(ISendMessages wrappedSender, ScenarioContext context)
        {
            this.wrappedSender = wrappedSender;
            sessionId = context.SessionId;
        }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            message.Headers[SessionCopInBehavior.Header] = sessionId;
            wrappedSender.Send(message, sendOptions);
        }
    }

    internal class DeferMessagesWrapper : IDeferMessages
    {
        private string sessionId;

        private IDeferMessages wrappedSender;

        public DeferMessagesWrapper(IDeferMessages wrappedSender, ScenarioContext context)
        {
            this.wrappedSender = wrappedSender;
            sessionId = context.SessionId;
        }

        public void Defer(TransportMessage message, SendOptions sendOptions)
        {
            message.Headers[SessionCopInBehavior.Header] = sessionId;
            wrappedSender.Defer(message, sendOptions);
        }

        public void ClearDeferredMessages(string headerKey, string headerValue)
        {
            wrappedSender.ClearDeferredMessages(headerKey, headerValue);
        }
    }

    internal class SessionCopInBehaviorForMainPipe : IBehavior<IncomingContext>
    {
        public const string Header = "SC.SessionID";
        private static string TestMessageId = Guid.Empty.ToString("N");
        private string sessionId;

        public SessionCopInBehaviorForMainPipe(ScenarioContext scenarioContext)
        {
            sessionId = scenarioContext.SessionId;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            string scSessionId;

            if (context.PhysicalMessage.Id == TestMessageId)
            {
                // This is the forwarding smoke test
                return;
            }

            if (!context.PhysicalMessage.Headers.TryGetValue(Header, out scSessionId))
            {
                return;
            }

            if (scSessionId == sessionId)
            {
                next();
            }
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("SessionCopInBehaviorForMainPipe", typeof(SessionCopInBehaviorForMainPipe), "Ignore messages from previous runs")
            {
                InsertBefore(WellKnownStep.ProcessingStatistics);
                InsertBeforeIfExists(WellKnownStep.AuditProcessedMessage);
            }
        }
    }

    internal class SessionCopInBehavior : IBehavior<IncomingContext>
    {
        public const string Header = "SC.SessionID";
        private string sessionId;

        public SessionCopInBehavior(ScenarioContext scenarioContext)
        {
            sessionId = scenarioContext.SessionId;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            string scSessionId;

            if (!context.PhysicalMessage.Headers.TryGetValue(Header, out scSessionId))
            {
                return;
            }

            if (scSessionId == sessionId)
            {
                next();
            }
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("SessionCopInBehavior", typeof(SessionCopInBehavior), "Ignore messages from previous runs")
            {
                InsertAfter(WellKnownStep.LoadHandlers);
                InsertBefore(WellKnownStep.InvokeHandlers);
            }
        }
    }
}