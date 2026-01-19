namespace ServiceControl.AcceptanceTesting.OpenIdConnect
{
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Net;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.IdentityModel.Tokens;

    /// <summary>
    /// A mock OpenID Connect server for acceptance testing.
    /// Provides OIDC discovery endpoints and can generate valid JWT tokens.
    /// </summary>
    public class MockOidcServer : IDisposable
    {
        readonly HttpListener listener;
        readonly RSA rsaKey;
        readonly RsaSecurityKey securityKey;
        readonly string keyId;
        readonly CancellationTokenSource cts = new();
        bool disposed;

        public string Authority { get; }
        public string Audience { get; }
        public int Port { get; }

        public MockOidcServer(int port = 0, string audience = "api://test-audience")
        {
            // Use a random port if 0 is specified
            Port = port == 0 ? GetAvailablePort() : port;
            Authority = $"http://localhost:{Port}";
            Audience = audience;

            // Generate RSA key pair for signing tokens
            rsaKey = RSA.Create(2048);
            keyId = Guid.NewGuid().ToString("N")[..16];
            securityKey = new RsaSecurityKey(rsaKey) { KeyId = keyId };

            listener = new HttpListener();
            listener.Prefixes.Add($"{Authority}/");
        }

        static int GetAvailablePort()
        {
            // Find an available port by binding to port 0
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Start()
        {
            listener.Start();
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (HttpListenerException) when (cts.Token.IsCancellationRequested)
                    {
                        // Expected when stopping
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected when stopping
                        break;
                    }
                }
            });
        }

        void HandleRequest(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? "";
            var response = context.Response;

            try
            {
                if (path == "/.well-known/openid-configuration")
                {
                    ServeDiscoveryDocument(response);
                }
                else if (path is "/.well-known/jwks" or "/jwks")
                {
                    ServeJwks(response);
                }
                else
                {
                    response.StatusCode = 404;
                    response.Close();
                }
            }
            catch
            {
                response.StatusCode = 500;
                response.Close();
            }
        }

        void ServeDiscoveryDocument(HttpListenerResponse response)
        {
            var discovery = new Dictionary<string, object>
            {
                ["issuer"] = Authority,
                ["authorization_endpoint"] = $"{Authority}/authorize",
                ["token_endpoint"] = $"{Authority}/token",
                ["jwks_uri"] = $"{Authority}/.well-known/jwks",
                ["response_types_supported"] = new[] { "code", "token", "id_token" },
                ["subject_types_supported"] = new[] { "public" },
                ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
                ["scopes_supported"] = new[] { "openid", "profile", "email" },
                ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_basic", "client_secret_post" },
                ["claims_supported"] = new[] { "sub", "iss", "aud", "exp", "iat", "name", "email" }
            };

            var json = JsonSerializer.Serialize(discovery);
            WriteJsonResponse(response, json);
        }

        void ServeJwks(HttpListenerResponse response)
        {
            var parameters = rsaKey.ExportParameters(false);

            var jwk = new Dictionary<string, object>
            {
                ["kty"] = "RSA",
                ["use"] = "sig",
                ["kid"] = keyId,
                ["alg"] = "RS256",
                ["n"] = Base64UrlEncode(parameters.Modulus),
                ["e"] = Base64UrlEncode(parameters.Exponent)
            };

            var jwks = new Dictionary<string, object>
            {
                ["keys"] = new[] { jwk }
            };

            var json = JsonSerializer.Serialize(jwks);
            WriteJsonResponse(response, json);
        }

        static void WriteJsonResponse(HttpListenerResponse response, string json)
        {
            response.ContentType = "application/json";
            response.StatusCode = 200;
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Generates a valid JWT token signed by this mock server.
        /// </summary>
        /// <param name="subject">The subject (sub) claim</param>
        /// <param name="expiresIn">Token lifetime</param>
        /// <param name="additionalClaims">Additional claims to include</param>
        /// <returns>A signed JWT token string</returns>
        public string GenerateToken(
            string subject = "test-user",
            TimeSpan? expiresIn = null,
            IEnumerable<Claim> additionalClaims = null)
        {
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims);
            }

            var token = new JwtSecurityToken(
                issuer: Authority,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(1)),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Generates an expired JWT token for testing token expiration.
        /// </summary>
        public string GenerateExpiredToken(string subject = "test-user")
        {
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: Authority,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires: DateTime.UtcNow.AddHours(-1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Generates a token with an invalid audience.
        /// </summary>
        public string GenerateTokenWithWrongAudience(string subject = "test-user")
        {
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: Authority,
                audience: "wrong-audience",
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                cts.Cancel();
                listener.Stop();
                listener.Close();
                rsaKey.Dispose();
                cts.Dispose();
                disposed = true;
            }
        }
    }
}
