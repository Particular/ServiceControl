namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Pipeline;

    class FullTypeNameOnlyBehavior : IBehavior<IOutgoingPhysicalMessageContext, IOutgoingPhysicalMessageContext>
    {

        public Task Invoke(IOutgoingPhysicalMessageContext context, Func<IOutgoingPhysicalMessageContext, Task> next)
        {
            var types = context.Headers[Headers.EnclosedMessageTypes];
            var assemblyFullName = typeof(FullTypeNameOnlyBehavior).Assembly.FullName;
            var enclosedTypes = types.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < enclosedTypes.Length; i++)
            {
                enclosedTypes[i] = enclosedTypes[i].Replace($", {assemblyFullName}", string.Empty);
            }
            context.Headers[Headers.EnclosedMessageTypes] = string.Join(";", enclosedTypes);
            
            return next(context);
        }
    }
}