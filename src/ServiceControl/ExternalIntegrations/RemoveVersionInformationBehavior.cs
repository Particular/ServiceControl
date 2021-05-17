namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Pipeline;

    public class RemoveVersionInformationBehavior : Behavior<IOutgoingPhysicalMessageContext>
    {
        public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
        {
            if (context.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out var typeHeader))
            {
                var types = typeHeader.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                var typesWithoutVersionInfo = string.Join(";", types.Select(x => RemoveVersionAndKeyInformation(x)));

                context.Headers[NServiceBus.Headers.EnclosedMessageTypes] = typesWithoutVersionInfo;
            }

            return next();
        }

        static string RemoveVersionAndKeyInformation(string qualifiedName)
        {
            if (qualifiedName.EndsWith(ContractsAssemblyIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                int startIndex = qualifiedName.IndexOf(VersionPart, StringComparison.Ordinal);
                if (startIndex >= 0)
                {
                    return qualifiedName.Substring(0, startIndex);
                }
            }

            return qualifiedName;
        }

        const string ContractsAssemblyIdentifier = "ServiceControl.Contracts, Version=3.0.0.0, Culture=neutral, PublicKeyToken=6f2d506f609d9f4d";
        const string VersionPart = ", Version=3.0.0.0, Culture=neutral, PublicKeyToken=6f2d506f609d9f4d";
    }
}
