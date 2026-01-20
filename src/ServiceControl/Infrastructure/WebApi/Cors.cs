namespace ServiceControl.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Cors.Infrastructure;

    /// <summary>
    /// Provides CORS (Cross-Origin Resource Sharing) policy configuration for the ServiceControl API.
    /// This enables ServicePulse and other web clients to make cross-origin requests to the API.
    /// </summary>
    static class Cors
    {
        /// <summary>
        /// Creates the default CORS policy based on the provided settings.
        /// </summary>
        /// <param name="settings">The CORS configuration settings.</param>
        /// <returns>A configured <see cref="CorsPolicy"/> instance.</returns>
        public static CorsPolicy GetDefaultPolicy(CorsSettings settings)
        {
            var builder = new CorsPolicyBuilder();

            // Configure origin restrictions based on settings.
            // When AllowAnyOrigin is true (the default), any origin can access the API.
            // When specific origins are configured, only those origins are allowed and credentials are permitted.
            if (settings.AllowAnyOrigin)
            {
                builder.AllowAnyOrigin();
            }
            else if (settings.AllowedOrigins.Count > 0)
            {
                builder.WithOrigins([.. settings.AllowedOrigins]);
                // Credentials (cookies, authorization headers) are only allowed with specific origins
                builder.AllowCredentials();
            }

            // Expose custom headers that clients need to read from responses
            builder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version", "Content-Disposition"]);

            // Allow standard headers required for API requests
            builder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept", "Authorization"]);

            // Allow all HTTP methods used by the ServiceControl API
            builder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD"]);

            return builder.Build();
        }
    }
}