namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;

    internal class DiscardMessagesBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        private ScenarioContext scenarioContext;

        public DiscardMessagesBehavior(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            string session;
            string intent;
            string messageTypes;
            //Do not filter out CC and HB messages as they can't be stamped
            if (context.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out messageTypes)
                && messageTypes.StartsWith("ServiceControl."))
            {
                return next(context);
            }

            //Do not filter out subscribe messages as they can't be stamped
            if (context.Message.Headers.TryGetValue(Headers.MessageIntent, out intent)
                && intent == MessageIntentEnum.Subscribe.ToString())
            {
                return next(context);
            }

            if (!context.Message.Headers.TryGetValue("SC.SessionID", out session) 
                || session != scenarioContext.TestRunId.ToString())
            {
                return Task.FromResult(0);
            }

            return next(context);
        }
    }
}