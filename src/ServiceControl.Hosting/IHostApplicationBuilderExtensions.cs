namespace ServiceControl.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

public static class IHostApplicationBuilderExtensions
{
    public static void AddWindowsServiceWithRequestTimeout(this IHostApplicationBuilder builder)
    {
        if (WindowsServiceHelpers.IsWindowsService())
        {
            builder.Services.AddWindowsService();
            builder.Services.AddSingleton<IHostLifetime, WindowsServiceWithRequestTimeout>();
        }
    }
}