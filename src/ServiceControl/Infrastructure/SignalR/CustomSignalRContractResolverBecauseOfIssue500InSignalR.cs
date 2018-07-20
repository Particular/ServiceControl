namespace ServiceControl.Infrastructure.SignalR
{
    using System;
    using System.Reflection;
    using Microsoft.AspNet.SignalR.Infrastructure;
    using Newtonsoft.Json.Serialization;

    public class CustomSignalRContractResolverBecauseOfIssue500InSignalR : IContractResolver
    {
        public CustomSignalRContractResolverBecauseOfIssue500InSignalR()
        {
            defaultContractSerializer = new DefaultContractResolver();
            underscoreContractResolver = new UnderscoreMappingResolver();
            assembly = typeof(Connection).Assembly;
        }

        public JsonContract ResolveContract(Type type)
        {
            if (type.Assembly.Equals(assembly))
            {
                return defaultContractSerializer.ResolveContract(type);
            }

            return underscoreContractResolver.ResolveContract(type);
        }

        private readonly Assembly assembly;
        private readonly IContractResolver underscoreContractResolver;
        private readonly IContractResolver defaultContractSerializer;
    }
}