namespace NServiceBus.AcceptanceTesting.Support
{
    using System;

    public interface IScenarioWithEndpointBehavior<TContext> where TContext : ScenarioContext
    {
        IScenarioWithEndpointBehavior<TContext> WithEndpoint<T>() where T : EndpointConfigurationBuilder;

        IScenarioWithEndpointBehavior<TContext> WithEndpoint<T>(Action<EndpointBehaviorBuilder<TContext>> behavior) where T : EndpointConfigurationBuilder;

        IScenarioWithEndpointBehavior<TContext> Done(Func<TContext, bool> func);

        TContext Run(TimeSpan? testExecutionTimeout = null);
    }
}