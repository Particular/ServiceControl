namespace ServiceControl.Infrastructure.DomainEvents
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;

    static class DomainEventsServiceCollectionExtensions
    {
        /// <summary>
        /// Registers provided type as event handler for all its implemented IDomainHandler interfaces as a transient component
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceCollection"></param>
        public static void AddDomainEventHandler<T>(this IServiceCollection serviceCollection)
        {
            serviceCollection.Add(new ServiceDescriptor(typeof(T), typeof(T), ServiceLifetime.Transient));
            var interfaces = typeof(T).GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDomainHandler<>)).ToArray();

            if (!interfaces.Any())
            {
                throw new Exception($"{nameof(AddDomainEventHandler)} requires registered type to implement at lest one {typeof(IDomainHandler<>).Name} interface.");
            }

            foreach (var serviceType in interfaces)
            {
                serviceCollection.Add(new ServiceDescriptor(serviceType, sp => sp.GetService(typeof(T)), ServiceLifetime.Transient));
            }
        }
    }
}