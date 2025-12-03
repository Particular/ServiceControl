namespace ServiceControl.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Cors.Infrastructure;

    static class Cors
    {
        public static CorsPolicy GetDefaultPolicy(CorsSettings settings)
        {
            var builder = new CorsPolicyBuilder();

            if (settings.AllowAnyOrigin)
            {
                builder.AllowAnyOrigin();
            }
            else if (settings.AllowedOrigins.Count > 0)
            {
                builder.WithOrigins([.. settings.AllowedOrigins]);
                builder.AllowCredentials();
            }

            builder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version", "Content-Disposition"]);
            builder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept", "Authorization"]);
            builder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD"]);

            return builder.Build();
        }
    }
}