﻿namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;

    public static class ScenarioWithEndpointBehaviorExtensions
    {
        public static IScenarioWithEndpointBehavior<TContext> Done<TContext>(this IScenarioWithEndpointBehavior<TContext> endpointBehavior, Func<TContext, Task<bool>> func) where TContext : ScenarioContext
        {
            var behavior = new ServiceControlClient<TContext>(func);

            return endpointBehavior.WithComponent(behavior).Done(ctx => behavior.Done);
        }

        public static EndpointBehaviorBuilder<TContext> When<TContext>(this EndpointBehaviorBuilder<TContext> endpointBehavior, Func<TContext, Task<bool>> predicate, Func<IMessageSession, TContext, Task> action) where TContext : ScenarioContext
        {
            return endpointBehavior.When(ctx => predicate(ctx).GetAwaiter().GetResult(), action);
        }

        public static SequenceBuilder<TContext> Do<TContext>(this IScenarioWithEndpointBehavior<TContext> endpointBehavior, string step, Func<TContext, Task<bool>> handler)
            where TContext : ScenarioContext, ISequenceContext
        {
            return new SequenceBuilder<TContext>(endpointBehavior, step, handler);
        }
    }

    public class SequenceBuilder<TContext>
        where TContext : ScenarioContext, ISequenceContext
    {
        public SequenceBuilder(IScenarioWithEndpointBehavior<TContext> endpointBehavior, string step, Func<TContext, Task<bool>> handler)
        {
            this.endpointBehavior = endpointBehavior;
            sequence.Do(step, handler);
        }

        public SequenceBuilder<TContext> Do(string step, Func<TContext, Task<bool>> handler)
        {
            sequence.Do(step, handler);
            return this;
        }

        public SequenceBuilder<TContext> Do(string step, Func<TContext, Task> handler)
        {
            sequence.Do(step, handler);
            return this;
        }

        public IScenarioWithEndpointBehavior<TContext> Done(Func<TContext, bool> doneCriteria = null)
        {
            var behavior = new ServiceControlClient<TContext>(context => sequence.Continue(context));
            return endpointBehavior.WithComponent(behavior).Done(ctx => sequence.IsFinished(ctx) && (doneCriteria == null || doneCriteria(ctx)));
        }

        IScenarioWithEndpointBehavior<TContext> endpointBehavior;
        Sequence<TContext> sequence = new Sequence<TContext>();
    }

    public class ServiceControlClient<TContext> : IComponentBehavior
        where TContext : ScenarioContext
    {
        public ServiceControlClient(Func<TContext, Task<bool>> checkDone)
        {
            this.checkDone = checkDone;
        }

        public bool Done => isDone;

        public Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            return Task.FromResult<ComponentRunner>(new Runner(checkDone, () => isDone = true, (TContext)run.ScenarioContext));
        }

        Func<TContext, Task<bool>> checkDone;
        volatile bool isDone;

        class Runner : ComponentRunner
        {
            public Runner(Func<TContext, Task<bool>> isDone, Action setDone, TContext scenarioContext)
            {
                this.isDone = isDone;
                this.setDone = setDone;
                this.scenarioContext = scenarioContext;
            }

            public override string Name => "ServiceControlClient";

            public override Task Start(CancellationToken token) => Task.FromResult(0);

            public override Task ComponentsStarted(CancellationToken token)
            {
                tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                checkTask = Task.Run(async () =>
                {
                    while (!tokenSource.IsCancellationRequested)
                    {
                        if (await isDone(scenarioContext).ConfigureAwait(false))
                        {
                            setDone();
                            return;
                        }

                        await Task.Delay(100, tokenSource.Token).ConfigureAwait(false);
                    }
                }, tokenSource.Token);
                return Task.FromResult(0);
            }

            public override async Task Stop()
            {
                if (checkTask == null)
                {
                    return;
                }

                tokenSource.Cancel();
                try
                {
                    await checkTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //Swallow
                }
                finally
                {
                    tokenSource.Dispose();
                }
            }

            Func<TContext, Task<bool>> isDone;
            Action setDone;
            TContext scenarioContext;
            Task checkTask;
            CancellationTokenSource tokenSource;
        }
    }
}