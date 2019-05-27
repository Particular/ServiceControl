namespace Particular.ServiceControl
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Pipeline;

    class PublishFullTypeNameOnlyBehavior : Behavior<IOutgoingPhysicalMessageContext>
    {

        public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
        {
            if (context.Headers[Headers.MessageIntent] != "Publish")
            {
                return next();
            }

            var types = context.Headers[Headers.EnclosedMessageTypes];
            var assemblyFullName = typeof(PublishFullTypeNameOnlyBehavior).Assembly.FullName;
            var enclosedTypes = types.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < enclosedTypes.Length; i++)
            {
                enclosedTypes[i] = enclosedTypes[i].Replace($", {assemblyFullName}", string.Empty);
            }
            context.Headers[Headers.EnclosedMessageTypes] = string.Join(";", enclosedTypes);
            return next();
        }
    }
}