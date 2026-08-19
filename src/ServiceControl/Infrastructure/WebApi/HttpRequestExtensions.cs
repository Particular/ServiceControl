namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using Microsoft.AspNetCore.Http;
    using Persistence.Infrastructure;

    static class HttpRequestExtensions
    {
        /// <summary>
        /// The version the caller already holds, or <see cref="DataVersion.None"/> if it holds none.
        /// </summary>
        public static DataVersion GetKnownVersion(this HttpRequest request)
        {
            // Read through typed headers, because If-None-Match is a comma separated list and reading the
            // raw header hands the whole list over as one malformed validator. A store can only skip work
            // for a single known version, so a caller holding several is treated as holding none, and so is
            // the "*" wildcard.
            var candidates = request.GetTypedHeaders().IfNoneMatch;

            return candidates is { Count: 1 } && !candidates[0].Tag.Equals("*", StringComparison.Ordinal)
                ? DataVersion.FromClient(candidates[0].ToString())
                : DataVersion.None;
        }
    }
}
