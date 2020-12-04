namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;

    static class HeaderFilter
    {
        public static Dictionary<string, string> RemoveErrorMessageHeaders(Dictionary<string, string> headers)
        {
            // still take a copy to preserve the old assumptions
            var headersToRetryWith = new Dictionary<string, string>(headers);
            foreach (var headerToRemove in KeysToRemoveWhenRetryingAMessage)
            {
                // iterate over original so that we are not running into modified collection problem
                foreach (var keyValuePair in headers)
                {
                    if (keyValuePair.Key.StartsWith(headerToRemove, StringComparison.Ordinal))
                    {
                        headersToRetryWith.Remove(keyValuePair.Key);
                    }
                }
            }
            return headersToRetryWith;
        }

        static readonly string[] KeysToRemoveWhenRetryingAMessage =
        {
            "NServiceBus.Retries",
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.",
            "NServiceBus.Timeout.Expire",
            "ServiceControl.EditOf"
        };
    }
}