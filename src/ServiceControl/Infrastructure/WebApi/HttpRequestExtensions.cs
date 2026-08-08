namespace ServiceControl.Infrastructure.WebApi
{
    using System.Linq;
    using Microsoft.AspNetCore.Http;
    using Persistence.Infrastructure;

    static class HttpRequestExtensions
    {
        /// <summary>
        /// The version the caller already holds, or <see cref="DataVersion.None"/> if it holds none.
        /// </summary>
        public static DataVersion GetKnownVersion(this HttpRequest request) =>
            DataVersion.FromClient(request.Headers.IfNoneMatch.FirstOrDefault());
    }
}
