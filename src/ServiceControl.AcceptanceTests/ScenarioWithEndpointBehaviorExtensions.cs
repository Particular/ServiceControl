namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;

    public static class ScenarioWithEndpointBehaviorExtensions
    {
        public static IScenarioWithEndpointBehavior<TContext> Done<TContext>(this IScenarioWithEndpointBehavior<TContext> endpointBehavior, Func<TContext, Task<bool>> func) where TContext : ScenarioContext
        {
            return endpointBehavior.Done(ctx => func(ctx).GetAwaiter().GetResult());
        }
    }
}