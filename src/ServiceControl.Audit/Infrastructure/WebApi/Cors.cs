﻿namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using Microsoft.AspNetCore.Cors.Infrastructure;

    static class Cors
    {
        public static CorsPolicy GetDefaultPolicy()
        {
            var builder = new CorsPolicyBuilder();

            builder.AllowAnyOrigin();
            builder.WithExposedHeaders(["ETag", "Last-Modified", "Link", "Total-Count", "X-Particular-Version"]);
            builder.WithHeaders(["Origin", "X-Requested-With", "Content-Type", "Accept"]);
            builder.WithMethods(["POST", "GET", "PUT", "DELETE", "OPTIONS", "PATCH"]);

            return builder.Build();
        }
    }
}