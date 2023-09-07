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
            switch (className)
            {
                case nameof(CustomCheck):
                    return typeof(CustomCheck);
                case nameof(CustomCheckDetail):
                    return typeof(CustomCheckDetail);
                case nameof(EndpointDetails):
                    return typeof(EndpointDetails);
                case nameof(ExceptionDetails):
                    return typeof(ExceptionDetails);
                case nameof(FailedMessage):
                    return typeof(FailedMessage);
                case nameof(FailureDetails):
                    return typeof(FailureDetails);
                case nameof(FailureGroup):
                    return typeof(FailureGroup);
                case nameof(GroupComment):
                    return typeof(GroupComment);
                case nameof(KnownEndpoint):
                    return typeof(KnownEndpoint);
                case nameof(ProcessedMessage):
                    return typeof(ProcessedMessage);
                case nameof(ProcessingAttempt):
                    return typeof(ProcessingAttempt);
                case nameof(FailedMessageRetry):
                    return typeof(FailedMessageRetry);
                case nameof(RetryBatch):
                    return typeof(RetryBatch);
                case nameof(RetryBatchNowForwarding):
                    return typeof(RetryBatchNowForwarding);
                default:
                    return base.BindToType(assemblyName, typeName);
            }
        }

        string GetClassName(string typeName)
        {
            return typeName.Split('.').Last();
        }
    }
}