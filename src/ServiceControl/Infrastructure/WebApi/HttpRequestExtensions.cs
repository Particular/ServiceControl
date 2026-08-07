namespace ServiceControl.Infrastructure.WebApi
{
    using System.Linq;
    using Microsoft.AspNetCore.Http;
    using Persistence.Infrastructure;

    static class HttpRequestExtensions
    {
        /// <summary>
        /// The version the caller already holds, or <see cref="DataVersion.None"/> if it holds none.
        /// <para>
        /// Only meaningful for an endpoint that publishes its version through
        /// <see cref="HttpResponseExtensions.WithEtag"/>. An endpoint publishing through
        /// <c>WithDeterministicEtag</c> hashes the validator on the way out, so what a client echoes
        /// back cannot be compared with anything a store holds and this would never match.
        /// </para>
        /// </summary>
        public static DataVersion GetKnownVersion(this HttpRequest request) =>
            DataVersion.FromClient(request.Headers.IfNoneMatch.FirstOrDefault());
    }
}
