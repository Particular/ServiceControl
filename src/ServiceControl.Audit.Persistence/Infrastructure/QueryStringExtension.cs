namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    static class QueryStringExtension
    {
        static readonly HttpRequestOptionsKey<Dictionary<string, string>> optionsKey = new("QueryStringAsDictionary");

        public static T GetQueryStringValue<T>(this HttpRequestMessage request, string key, T defaultValue = default)
        {
            if (!request.Options.TryGetValue(optionsKey, out var queryStringDictionary))
            {
                queryStringDictionary = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                request.Options.Set(optionsKey, queryStringDictionary);
            }

            queryStringDictionary.TryGetValue(key, out var value);

            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
    }
}