namespace ServiceControl.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NUnit.Framework;
using NUnit.Framework.Internal;

public static class TestTimeoutScenarioExtensions
{
    /// <summary>
    /// Bounds the scenario by the test's own cancellation token instead of the framework's fixed 90 second
    /// <c>Done</c> limit. The token comes from <see cref="CancelAfterAttribute"/>, which
    /// <see cref="NServiceBusAcceptanceTest"/> applies to every test and which a test can override.
    /// </summary>
    public static IScenarioWithEndpointBehavior<TContext> WithTestTimeout<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario)
        where TContext : ScenarioContext =>
        new TestTimeoutScenario<TContext>(scenario);
}

class TestTimeoutScenario<TContext>(IScenarioWithEndpointBehavior<TContext> inner) : IScenarioWithEndpointBehavior<TContext>
    where TContext : ScenarioContext
{
    public IScenarioWithEndpointBehavior<TContext> WithEndpoint<T>() where T : EndpointConfigurationBuilder, new()
    {
        inner = inner.WithEndpoint<T>();
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithEndpoint<T>(Action<EndpointBehaviorBuilder<TContext>> behavior) where T : EndpointConfigurationBuilder, new()
    {
        inner = inner.WithEndpoint<T>(behavior);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithEndpoint(EndpointConfigurationBuilder endpointConfigurationBuilder, Action<EndpointBehaviorBuilder<TContext>> defineBehavior)
    {
        inner = inner.WithEndpoint(endpointConfigurationBuilder, defineBehavior);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithComponent(IComponentBehavior componentBehavior)
    {
        inner = inner.WithComponent(componentBehavior);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithServices(Action<IServiceCollection> configureServices)
    {
        inner = inner.WithServices(configureServices);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithServices(Action<IServiceCollection, TContext> configureServices)
    {
        inner = inner.WithServices(configureServices);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithServiceResolve(Func<IServiceProvider, CancellationToken, Task> resolve, ServiceResolveMode resolveMode = ServiceResolveMode.BeforeStart)
    {
        inner = inner.WithServiceResolve(resolve, resolveMode);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> WithServiceResolve(Func<IServiceProvider, TContext, CancellationToken, Task> resolve, ServiceResolveMode resolveMode = ServiceResolveMode.BeforeStart)
    {
        inner = inner.WithServiceResolve(resolve, resolveMode);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> Done(Func<TContext, bool> func)
    {
        inner = inner.Done(func);
        return this;
    }

    public IScenarioWithEndpointBehavior<TContext> Done(Func<TContext, TaskCompletionSource> func)
    {
        inner = inner.Done(func);
        return this;
    }

    public Task<TContext> Run(CancellationToken cancellationToken = default) => Run(new RunSettings(), cancellationToken);

    public async Task<TContext> Run(RunSettings settings, CancellationToken cancellationToken = default)
    {
        // The framework only lifts its fixed 90 second Done limit when handed a token that can actually be
        // cancelled, so a caller that passes nothing gets the test's own token instead.
        if (!cancellationToken.CanBeCanceled)
        {
            cancellationToken = TestContext.CurrentContext.CancellationToken;
        }

        try
        {
            return await inner.Run(settings, cancellationToken);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            // Done wraps the cancellation in a TimeoutException naming the framework's own limit, which is
            // infinite for a cancellable token and prints as a negative number of seconds.
            throw new TimeoutException($"The scenario did not complete within the test's CancelAfter budget of {TestExecutionContext.CurrentContext.TestCaseTimeout} ms.", e);
        }
    }
}
