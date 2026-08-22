namespace ServiceControl.Infrastructure.Health
{
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;

    static class HealthCheckExtensions
    {
        public const string LivenessPath = "/health";
        public const string ReadinessPath = "/health/ready";

        internal const string ReadyTag = "ready";

        // The individual checks are added by the components that host the work they report on, so a
        // host that does not ingest error messages does not answer for error ingestion.
        public static void AddServiceControlHealthChecks(this IServiceCollection services) =>
            services.AddHealthChecks();

        /// <summary>
        /// Liveness answers "is this process still serving", and is what a container health check
        /// should restart on. Readiness additionally reports whether the work this host exists to do
        /// is actually happening, which is a poor reason to kill a container but the right thing for
        /// an operator or a load balancer to look at.
        /// </summary>
        public static void MapServiceControlHealthChecks(this WebApplication app)
        {
            app.MapHealthChecks(LivenessPath, new HealthCheckOptions
            {
                Predicate = _ => false,
                ResponseWriter = WriteResponse
            }).AllowAnonymous();

            app.MapHealthChecks(ReadinessPath, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(ReadyTag),
                ResponseWriter = WriteResponse
            }).AllowAnonymous();
        }

        // The container health check binary rejects anything that is not non-empty JSON, so the
        // default plain text writer cannot be used here.
#pragma warning disable PS0018 // The signature is fixed by HealthCheckOptions.ResponseWriter.
        static Task WriteResponse(HttpContext context, HealthReport report)
#pragma warning restore PS0018
        {
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(JsonSerializer.Serialize(new HealthResponse
            {
                Status = report.Status.ToString(),
                Checks = [.. report.Entries.Select(entry => new HealthResponse.Check
                {
                    Name = entry.Key,
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description ?? entry.Value.Exception?.Message
                })]
            }, SerializerOptions), context.RequestAborted);
        }

        static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        class HealthResponse
        {
            public required string Status { get; init; }
            public required Check[] Checks { get; init; }

            public class Check
            {
                public required string Name { get; init; }
                public required string Status { get; init; }
                public string Description { get; init; }
            }
        }
    }
}
