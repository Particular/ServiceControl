namespace ServiceControl.Monitoring;

using System;
using System.Threading.Tasks;
using Messaging;
using NServiceBus.Pipeline;

class MessagePoolReleasingBehavior : Behavior<IIncomingLogicalMessageContext>
{
    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        try
        {
            await next();
        }
        finally
        {
            var messageType = context.Message.MessageType;
            var instance = context.Message.Instance;

            if (messageType == typeof(TaggedLongValueOccurrence))
            {
                ReleaseMessage<TaggedLongValueOccurrence>(instance);
            }
        }
    }

    static void ReleaseMessage<T>(object instance) where T : RawMessage, new()
    {
        RawMessage.Pool<T>.Default.Release((T)instance);
    }
}