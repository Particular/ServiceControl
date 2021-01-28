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
            var enclosedTypes = types.Split(Semicolon, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < enclosedTypes.Length; i++)
            {
                enclosedTypes[i] = enclosedTypes[i].Replace($", {AssemblyFullName}", string.Empty);
            }

            context.Headers[Headers.EnclosedMessageTypes] = string.Join(";", enclosedTypes);

            return next(context);
        }

        static string AssemblyFullName = typeof(FullTypeNameOnlyBehavior).Assembly.FullName;
        static string[] Semicolon = { ";" };
    }
}