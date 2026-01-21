namespace ServiceControl.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Cors.Infrastructure;

    /// <summary>
    /// Provides CORS (Cross-Origin Resource Sharing) policy configuration for the ServiceControl API.
    /// This enables ServicePulse and other web clients to make cross-origin requests to the API.
    /// </summary>
    static class Cors
    {
        public static CorsPolicy GetDefaultPolicy(CorsSettings settings)
        {
            var builder = new CorsPolicyBuilder();

            // When AllowAnyOrigin is true (the default), any origin can access the API.
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