namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;

    public class RemoveVersionInformationBehavior : Behavior<IOutgoingPhysicalMessageContext>
    {
        public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
        {
            if (context.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out var qualifiedTypeName)
                && qualifiedTypeName.EndsWith(ContractsAssemblyIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                int startIndex = qualifiedTypeName.IndexOf(VersionPart, StringComparison.Ordinal);
                if (startIndex >= 0)
                {
                    qualifiedTypeName = qualifiedTypeName.Substring(0, startIndex);
                    context.Headers[NServiceBus.Headers.EnclosedMessageTypes] = qualifiedTypeName;
                }
            }

            return next();
        }

        const string ContractsAssemblyIdentifier = "ServiceControl.Contracts, Version=3.0.0.0, Culture=neutral, PublicKeyToken=6f2d506f609d9f4d";
        const string VersionPart = ", Version=3.0.0.0, Culture=neutral, PublicKeyToken=6f2d506f609d9f4d";
    }
}
