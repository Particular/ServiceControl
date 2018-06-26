namespace ServiceBus.Management.AcceptanceTests
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
    }

    public class ServiceControlClient<TContext> : IComponentBehavior
        where TContext: ScenarioContext
    {
        Func<TContext, Task<bool>> checkDone;
        volatile bool isDone;

        public ServiceControlClient(Func<TContext, Task<bool>> checkDone)
        {
            this.checkDone = checkDone;
        }

        public bool Done => isDone;

        public Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            return Task.FromResult<ComponentRunner>(new Runner(checkDone, () => isDone = true, (TContext)run.ScenarioContext));
        }

        class Runner : ComponentRunner
        {
            Func<TContext, Task<bool>> isDone;
            Action setDone;
            TContext scenarioContext;
            Task checkTask;
            CancellationTokenSource tokenSource;

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
                tokenSource = new CancellationTokenSource();
                var done = false;
                checkTask = Task.Run(async () =>
                {
                    while (!done && !tokenSource.IsCancellationRequested)
                    {
                        done = await isDone(scenarioContext).ConfigureAwait(false);
                        if (done)
                        {
                            setDone();
                        }
                        else
                        {
                            await Task.Delay(100).ConfigureAwait(false);
                        }
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
                catch (TaskCanceledException)
                {
                    //Swallow
                }
            }
        }
    }
}
