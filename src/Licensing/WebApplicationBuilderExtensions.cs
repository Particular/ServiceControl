namespace Particular.Licensing;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

static class WebApplicationBuilderExtensions
{
    public static void AddThroughputCollection(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddHostedService<ThroughputCalculatorHostedService>();
    }
}