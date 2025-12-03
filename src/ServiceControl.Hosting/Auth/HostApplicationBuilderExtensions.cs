namespace ServiceControl.Hosting.Auth
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.IdentityModel.Tokens;
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

                // Custom error response handling for better client experience
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers.Append("X-Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // Skip if response already started or already handled
                        if (context.Response.HasStarted || context.Handled)
                        {
                            return Task.CompletedTask;
                        }

                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";

                        var errorResponse = new AuthErrorResponse
                        {
                            Error = "unauthorized",
                            Message = GetErrorMessage(context)
                        };

                        return context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonSerializerOptions));
                    },
                    OnForbidden = context =>
                    {
                        // Skip if response already started
                        if (context.Response.HasStarted)
                        {
                            return Task.CompletedTask;
                        }

                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";

                        var errorResponse = new AuthErrorResponse
                        {
                            Error = "forbidden",
                            Message = "You do not have permission to access this resource."
                        };

                        return context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonSerializerOptions));
                    }
                };
            });
        }

        static string GetErrorMessage(JwtBearerChallengeContext context)
        {
            if (context.AuthenticateFailure is SecurityTokenExpiredException)
            {
                return "The token has expired. Please obtain a new token and retry.";
            }

            if (context.AuthenticateFailure is SecurityTokenInvalidSignatureException)
            {
                return "The token signature is invalid.";
            }

            if (context.AuthenticateFailure is SecurityTokenInvalidAudienceException)
            {
                return "The token audience is invalid.";
            }

            if (context.AuthenticateFailure is SecurityTokenInvalidIssuerException)
            {
                return "The token issuer is invalid.";
            }

            if (context.AuthenticateFailure != null)
            {
                return "The token is invalid.";
            }

            return "Authentication required. Please provide a valid Bearer token.";
        }

        static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    class AuthErrorResponse
    {
        public string Error { get; set; }
        public string Message { get; set; }
    }
}
