namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Cors.Infrastructure;
    using ServiceControl.Infrastructure;

    /// <summary>
    /// Configures Cross-Origin Resource Sharing (CORS) policy for the ServiceControl.Audit API.
    /// CORS allows the API to be called from web applications hosted on different origins (e.g., ServicePulse).
    /// </summary>
    static class Cors
    {
        /// <summary>
        /// Builds the default CORS policy based on the provided settings.
        /// </summary>
        public static CorsPolicy GetDefaultPolicy(CorsSettings settings)
        {
            var builder = new CorsPolicyBuilder();

            // Configure allowed origins based on settings
            if (settings.AllowAnyOrigin)
            {
                // Allow requests from any origin (less secure, useful for development)
                builder.AllowAnyOrigin();
            }
            else if (settings.AllowedOrigins.Count > 0)
            {
                // Allow only specific origins (more secure, recommended for production)
                builder.WithOrigins([.. settings.AllowedOrigins]);
                // Allow credentials (cookies, authorization headers) when specific origins are configured
                builder.AllowCredentials();
            }

            // Headers exposed to the client in the response (accessible via JavaScript)
            builder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"]);
            // Headers allowed in the request from the client
            builder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept", "Authorization"]);
            // HTTP methods allowed for cross-origin requests
            builder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD"]);

            return builder.Build();
        }
    }
}