namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;

    static class HeaderFilter
    {
        public static Dictionary<string, string> RemoveErrorMessageHeaders(Dictionary<string, string> headers)
        {
            var headersToRetryWith = headers
                .Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return headersToRetryWith;
        }

        static readonly string[] KeysToRemoveWhenRetryingAMessage =
        {
            "NServiceBus.Retries",
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace",
            "NServiceBus.ExceptionInfo.HelpLink",
            "NServiceBus.ExceptionInfo.Message",
            "ServiceControl.EditOf",
            "NServiceBus.ExceptionInfo.Data.Message type",
            "NServiceBus.ExceptionInfo.Data.Handler type",
            "NServiceBus.ExceptionInfo.Data.Handler start time",
            "NServiceBus.ExceptionInfo.Data.Handler failure time",
            "NServiceBus.ExceptionInfo.Data.Message ID",
            "NServiceBus.ExceptionInfo.Data.Transport message ID",
            "NServiceBus.Retries.Timestamp",
            "NServiceBus.Timeout.Expire"
        };
    }
}