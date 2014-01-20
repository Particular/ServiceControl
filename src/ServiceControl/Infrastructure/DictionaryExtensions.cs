namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Generic;

    public static class DictionaryExtensions
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