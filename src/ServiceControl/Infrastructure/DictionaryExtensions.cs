namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Generic;

    public static class DictionaryExtensions
    {
        public static void CheckIfKeyExists(string key, IReadOnlyDictionary<string, string> headers, Action<string> actionToInvokeWhenKeyIsFound)
        {
            string value;
            if (headers.TryGetValue(key, out value))
            {
                actionToInvokeWhenKeyIsFound(value);
            }
        }
    }
}