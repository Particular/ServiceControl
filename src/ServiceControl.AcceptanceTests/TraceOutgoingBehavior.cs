namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;

    class TraceOutgoingBehavior : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
    {
        string endpointName;

        public TraceOutgoingBehavior(string endpointName)
        {
            this.endpointName = endpointName;
        }

        public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
        {
            StaticLoggerFactory.CurrentContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Endpoint = endpointName,
                Level = LogLevel.Info,
                LoggerName = "Trace",
                Message = $"-> {context.Headers[Headers.MessageIntent]} {context.Message.MessageType.Name} ({context.Headers[Headers.MessageId].Substring(context.Headers[Headers.MessageId].Length - 4)})"
            });
            return next(context);
        }
    }
}