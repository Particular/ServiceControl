namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.IdentityModel.JsonWebTokens;
    using Microsoft.IdentityModel.Tokens;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");

            settings.RunCleanupBundle = true;

            var hostBuilder = WebApplication.CreateBuilder();

            // Configure JWT Bearer Authentication with OpenID Connect
            if (settings.OpenIdConnectSettings.Enabled)
            {
                hostBuilder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    var oidcSettings = settings.OpenIdConnectSettings;

                    options.Authority = oidcSettings.Authority;

                    // Configure token validation parameters
                    options.TokenValidationParameters = new TokenValidationParameters
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

                // Clear the default claim type mappings to use standard JWT claim names
                JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();
            }


            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi();

            var app = hostBuilder.Build();
            app.UseServiceControl(authenticationEnabled: settings.OpenIdConnectSettings.Enabled);

            await app.RunAsync(settings.RootUrl);
        }
    }
}
