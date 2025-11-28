namespace ServiceControl.Hosting.Auth
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceControl.Infrastructure;

    public static class HostApplicationBuilderExtensions
    {
        public static void AddServiceControlAuthentication(this IHostApplicationBuilder hostBuilder, OpenIdConnectSettings oidcSettings)
        {
            if (!oidcSettings.Enabled)
            {
                return;
            }

            hostBuilder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "Bearer";
                options.DefaultChallengeScheme = "Bearer";
            })
            .AddJwtBearer("Bearer", options =>
            {
                options.Authority = oidcSettings.Authority;
                // Configure token validation parameters
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = oidcSettings.ValidateIssuer,
                    ValidateAudience = oidcSettings.ValidateAudience,
                    ValidateLifetime = oidcSettings.ValidateLifetime,
                    ValidateIssuerSigningKey = oidcSettings.ValidateIssuerSigningKey,
                    ValidAudience = oidcSettings.Audience,
                    ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
                };
                options.RequireHttpsMetadata = oidcSettings.RequireHttpsMetadata;
                // Don't map inbound claims to legacy Microsoft claim types
                options.MapInboundClaims = false;
            });
        }
    }
}
