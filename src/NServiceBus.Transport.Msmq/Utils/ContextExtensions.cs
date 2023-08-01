namespace NServiceBus.Transport.Msmq
{
    using Extensibility;

    static class ContextExtensions
    {
        // Implements the 'ExtendableOptionsExtensions.GetOperationProperties' extension from Core to prevent this patch to force an update to Core 7.7 (as this has also been fixed on 7.5 and 7.6).
        public static ReadOnlyContextBag GetOperationProperties(this ContextBag contextBag)
        {
            if (contextBag.TryGet("NServiceBus.OperationProperties", out ContextBag operationProperties))
            {
                return operationProperties;
            }

            return contextBag; // fallback behavior, e.g. when invoking the outgoing pipeline without using MessageOperation API.
        }
    }
}