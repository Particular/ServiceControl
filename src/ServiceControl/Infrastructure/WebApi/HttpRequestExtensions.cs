namespace ServiceControl.Infrastructure.WebApi
{
    using System.Linq;
    using Microsoft.AspNetCore.Http;

    static class HttpRequestExtensions
    {
        /// <summary>
        /// The validator the caller already holds, unquoted so it can be compared against a store's
        /// own version, or <c>null</c> if the caller holds none.
        /// <para>
        /// Only meaningful for an endpoint that publishes its validator through
        /// <see cref="HttpResponseExtensions.WithEtag"/>. An endpoint publishing through
        /// <c>WithDeterministicEtag</c> hashes the validator on the way out, so what a client echoes
        /// back cannot be compared with anything a store holds and this would never match.
        /// </para>
        /// </summary>
        public static string GetKnownVersion(this HttpRequest request) =>
            Unquote(request.Headers.IfNoneMatch.FirstOrDefault());

        // Trimming every quote instead would turn a malformed header
        // into a truncated value rather than into the cache miss it should be.
        static string Unquote(string etag) =>
            etag?.Length > 1 && etag[0] == '"' && etag[^1] == '"' ? etag[1..^1] : etag;
    }
}
