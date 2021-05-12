namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;

    public class FallbackToOldContractTypes : Behavior<IOutgoingPhysicalMessageContext>
    {
        public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
        {

            if (context.Headers.TryGetValue(EnclosedTypesHeaderName, out var qualifiedTypeName))
            {
                if (qualifiedTypeName.EndsWith(NewAssemblyIdentifier))
                {
                    var typeName = qualifiedTypeName.Split(',')[0];

                    context.Headers[EnclosedTypesHeaderName] = $"{typeName}, {OldAssemblyIdentifier}";
                }
            }

            return next();
        }

        const string EnclosedTypesHeaderName = "NServiceBus.EnclosedMessageTypes";

        const string NewAssemblyIdentifier = "ServiceControl.Contracts, Version=3.0.0.0, Culture=neutral, PublicKeyToken=6f2d506f609d9f4d";
        const string OldAssemblyIdentifier = "ServiceControl.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
    }
}