namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;

    public static class ScenarioWithEndpointBehaviorExtensions
    {
        public static IScenarioWithEndpointBehavior<TContext> Done<TContext>(this IScenarioWithEndpointBehavior<TContext> endpointBehavior, Func<TContext, Task<bool>> func) where TContext : ScenarioContext
        {
            return endpointBehavior.Done(ctx => func(ctx).GetAwaiter().GetResult());
        }

        public static EndpointBehaviorBuilder<TContext> When<TContext>(this EndpointBehaviorBuilder<TContext> endpointBehavior, Func<TContext, Task<bool>> predicate, Func<IMessageSession, TContext, Task> action) where TContext : ScenarioContext
        {
            return endpointBehavior.When(ctx => predicate(ctx).GetAwaiter().GetResult(), action);
        }
    }
}