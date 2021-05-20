namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Raven.Client.Linq;

    public static class RavenQueryableExtensions
    {
        public static IRavenQueryable<TSource> Paging<TSource>(this IRavenQueryable<TSource> source, HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);

            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        static T GetQueryStringValue<T>(this HttpRequestMessage request, string key, T defaultValue = default)
        {
            Dictionary<string, string> queryStringDictionary;
            if (!request.Properties.TryGetValue("QueryStringAsDictionary", out var dictionaryAsObject))
            {
                queryStringDictionary = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                request.Properties["QueryStringAsDictionary"] = queryStringDictionary;
            }
            else
            {
                queryStringDictionary = (Dictionary<string, string>)dictionaryAsObject;
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