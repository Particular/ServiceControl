namespace Particular.ServiceControl
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using global::ServiceControl.Infrastructure.DomainEvents;

    static class ComponentActivatorExtensions
    {
        public static ComponentActivator CreateActivator<T>(this IComponent<T> component) 
            where T : class, new()
        {
            return new ComponentActivator<T>(component);
        }
    }

    public abstract class ComponentActivator
    {
        public abstract void RegisterDependency(ContainerBuilder containerBuilder);
        public abstract Task Initialize(IContainer container);
        public abstract Task TearDown();

        public abstract IEnumerable<object> CreateParts();
    }

    class ComponentActivator<T> : ComponentActivator
        where T : class, new()
    {
        IComponent<T> component;

        public ComponentActivator(IComponent<T> component)
        {
            this.component = component;
        }

        public override void RegisterDependency(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterType<T>().SingleInstance().PropertiesAutowired();
        }

        public override Task Initialize(IContainer container)
        {
            var dependency = container.Resolve<T>();
            return component.Initialize(dependency);
        }

        public override Task TearDown()
        {
            return component.TearDown();
        }

        public override IEnumerable<object> CreateParts()
        {
            return component.CreateParts();
        }
    }
}