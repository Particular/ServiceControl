namespace ServiceControl.AcceptanceTesting.OpenIdConnect
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Shared assertion helpers for OpenID Connect acceptance tests.
    /// Used across all instance types (Primary, Audit, Monitoring).
    /// </summary>
    public static class OpenIdConnectAssertions
    {
        public const string AuthorizationHeader = "Authorization";
        public const string WwwAuthenticateHeader = "WWW-Authenticate";
        public const string XTokenExpiredHeader = "X-Token-Expired";

        /// <summary>
        /// Asserts that the response indicates successful authentication/authorization.
        /// </summary>
        public static void AssertAuthenticated(HttpResponseMessage response)
        {
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized).And.Not.EqualTo(HttpStatusCode.Forbidden),
                "Response should not be 401 Unauthorized or 403 Forbidden when properly authenticated");
        }

        /// <summary>
        /// Asserts that the response indicates authentication is required (401 Unauthorized).
        /// </summary>
        public static void AssertUnauthorized(HttpResponseMessage response)
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Response should be 401 Unauthorized when authentication is required but not provided");
        }

        /// <summary>
        /// Asserts that the response indicates forbidden access (403 Forbidden).
        /// </summary>
        public static void AssertForbidden(HttpResponseMessage response)
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden),
                "Response should be 403 Forbidden when the user lacks required permissions");
        }

        /// <summary>
        /// Asserts that the response does not require authentication (for when auth is disabled).
        /// </summary>
        public static void AssertNoAuthenticationRequired(HttpResponseMessage response)
        {
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized),
                "Response should not require authentication when OpenID Connect is disabled");
        }

        /// <summary>
        /// Asserts that the response contains the WWW-Authenticate header with Bearer scheme.
        /// </summary>
        public static void AssertWwwAuthenticateHeader(HttpResponseMessage response)
        {
            Assert.That(response.Headers.WwwAuthenticate, Is.Not.Empty,
                "Response should contain WWW-Authenticate header");

            var hasBearer = false;
            foreach (var header in response.Headers.WwwAuthenticate)
            {
                if (header.Scheme == "Bearer")
                {
                    hasBearer = true;
                    break;
                }
            }

            Assert.That(hasBearer, Is.True,
                "WWW-Authenticate header should specify Bearer scheme");
        }

        /// <summary>
        /// Asserts that the response body contains the expected error response format.
        /// </summary>
        public static async Task AssertAuthErrorResponse(HttpResponseMessage response, string expectedError = "unauthorized")
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty, "Response should have a body");

            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.TryGetProperty("error", out var errorProperty), Is.True,
                            "Response should contain 'error' property");
                Assert.That(errorProperty.GetString(), Is.EqualTo(expectedError),
                    $"Error should be '{expectedError}'");

                Assert.That(root.TryGetProperty("message", out var messageProperty), Is.True,
                    "Response should contain 'message' property");
                Assert.That(messageProperty.GetString(), Is.Not.Null.And.Not.Empty,
                    "Message should not be empty");
            }
        }

        /// <summary>
        /// Asserts that the authentication configuration endpoint returns expected values.
        /// </summary>
        public static async Task AssertAuthConfigurationResponse(
            HttpResponseMessage response,
            bool expectedEnabled,
            string expectedClientId = null,
            string expectedAuthority = null,
            string expectedAudience = null,
            string expectedApiScopes = null)
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Authentication configuration endpoint should return 200 OK");

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(root.TryGetProperty("enabled", out var enabledProperty), Is.True,
                            "Response should contain 'enabled' property");
                Assert.That(enabledProperty.GetBoolean(), Is.EqualTo(expectedEnabled),
                    $"'enabled' should be {expectedEnabled}");
            }

            // Note: API uses snake_case JSON serialization (client_id, api_scopes)
            if (expectedClientId != null)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(root.TryGetProperty("client_id", out var clientIdProperty), Is.True,
                                    "Response should contain 'client_id' property");
                    Assert.That(clientIdProperty.GetString(), Is.EqualTo(expectedClientId),
                        $"'client_id' should be '{expectedClientId}'");
                }
            }

            if (expectedAuthority != null)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(root.TryGetProperty("authority", out var authorityProperty), Is.True,
                                    "Response should contain 'authority' property");
                    Assert.That(authorityProperty.GetString(), Is.EqualTo(expectedAuthority),
                        $"'authority' should be '{expectedAuthority}'");
                }
            }

            if (expectedAudience != null)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(root.TryGetProperty("audience", out var audienceProperty), Is.True,
                                    "Response should contain 'audience' property");
                    Assert.That(audienceProperty.GetString(), Is.EqualTo(expectedAudience),
                        $"'audience' should be '{expectedAudience}'");
                }
            }

            if (expectedApiScopes != null)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(root.TryGetProperty("api_scopes", out var apiScopesProperty), Is.True,
                                    "Response should contain 'api_scopes' property");
                    Assert.That(apiScopesProperty.GetString(), Is.EqualTo(expectedApiScopes),
                        $"'api_scopes' should be '{expectedApiScopes}'");
                }
            }
        }

        /// <summary>
        /// Creates an Authorization header with a Bearer token.
        /// </summary>
        public static AuthenticationHeaderValue CreateBearerToken(string token)
        {
            return new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Sends a request with a Bearer token.
        /// </summary>
        public static async Task<HttpResponseMessage> SendRequestWithBearerToken(
            HttpClient client,
            HttpMethod method,
            string path,
            string token)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = CreateBearerToken(token);
            return await client.SendAsync(request);
        }

        /// <summary>
        /// Sends a request without any authentication.
        /// </summary>
        public static async Task<HttpResponseMessage> SendRequestWithoutAuth(
            HttpClient client,
            HttpMethod method,
            string path)
        {
            using var request = new HttpRequestMessage(method, path);
            return await client.SendAsync(request);
        }
    }
}
