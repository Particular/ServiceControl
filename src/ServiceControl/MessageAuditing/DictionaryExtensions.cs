namespace ServiceBus.Management.MessageAuditing
{
    using System;
    using System.Collections.Generic;

    static class DictionaryExtensions
    {
        public static void CheckIfKeyExists(string key, IDictionary<string, string> headers, Action<string> actionToInvokeWhenKeyIsFound)
        {
            var value = string.Empty;
            if (headers.TryGetValue(key, out value))
            {
                actionToInvokeWhenKeyIsFound(value);
            }
        }
    }
}