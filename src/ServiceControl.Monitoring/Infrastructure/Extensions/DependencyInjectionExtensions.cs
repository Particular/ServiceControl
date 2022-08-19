namespace ServiceControl.Monitoring.Infrastructure.Extensions
{
    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection RegisterAsSelfAndImplementedInterfaces<TClass>(this IServiceCollection services)
            where TClass : class
        {
            services.AddSingleton<TClass>();
            services.RegisterAsImplementedInterfaces<TClass>();

            return services;
        }

        public static IServiceCollection RegisterAsImplementedInterfaces<TClass>(this IServiceCollection services)
            where TClass : class
        {
            var interfaces = typeof(TClass).GetInterfaces();

            foreach (var type in interfaces)
            {
                services.AddSingleton(type, s => s.GetRequiredService<TClass>());
            }

            return services;
        }
    }
}