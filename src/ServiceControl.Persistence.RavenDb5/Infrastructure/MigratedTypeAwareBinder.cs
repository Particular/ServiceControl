namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Linq;
    using Newtonsoft.Json.Serialization;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;
    using static ServiceControl.MessageFailures.FailedMessage;

    class MigratedTypeAwareBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            var className = GetClassName(typeName);
            return className switch
            {
                nameof(CustomCheck) => typeof(CustomCheck),
                nameof(CustomCheckDetail) => typeof(CustomCheckDetail),
                nameof(EndpointDetails) => typeof(EndpointDetails),
                nameof(ExceptionDetails) => typeof(ExceptionDetails),
                nameof(FailedMessage) => typeof(FailedMessage),
                nameof(FailureDetails) => typeof(FailureDetails),
                nameof(FailureGroup) => typeof(FailureGroup),
                nameof(GroupComment) => typeof(GroupComment),
                nameof(KnownEndpoint) => typeof(KnownEndpoint),
                nameof(ProcessedMessage) => typeof(ProcessedMessage),
                nameof(ProcessingAttempt) => typeof(ProcessingAttempt),
                nameof(FailedMessageRetry) => typeof(FailedMessageRetry),
                nameof(RetryBatch) => typeof(RetryBatch),
                nameof(RetryBatchNowForwarding) => typeof(RetryBatchNowForwarding),
                _ => base.BindToType(assemblyName, typeName),
            };
        }

        string GetClassName(string typeName)
        {
            return typeName.Split('.').Last();
        }
    }
}