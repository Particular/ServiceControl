namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Runtime.ExceptionServices;
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

        public static EndpointBehaviorBuilder<TContext> When<TContext>(this EndpointBehaviorBuilder<TContext> endpointBehavior, Func<TContext, Task<bool>> predicate, Func<IMessageSession, TContext, Task> action) where TContext : ScenarioContext => endpointBehavior.When(ctx => predicate(ctx).GetAwaiter().GetResult(), action);

        public static SequenceBuilder<TContext> Do<TContext>(
            this IScenarioWithEndpointBehavior<TContext> endpointBehavior, string step,
            Func<TContext, Task<bool>> handler) where TContext : ScenarioContext, ISequenceContext =>
            new(endpointBehavior, step, handler);
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
            return endpointBehavior.WithComponent(behavior).Done(async ctx =>
            {
                if (!behavior.Done && !sequence.IsFinished(ctx))
                {
                    return false;
                }

                if (doneCriteria == null || doneCriteria(ctx))
                {
                    return true;
                }

                // If sequence is done but test is not finished, small delay to avoid tight loop check
                await Task.Delay(250);

                // If sequence is not finished immediately return false, since each step will enforce delays
                return false;
            });
        }

        IScenarioWithEndpointBehavior<TContext> endpointBehavior;
        Sequence<TContext> sequence = new Sequence<TContext>();
    }

    public class ServiceControlClient<TContext>(Func<TContext, Task<bool>> checkDone) : IComponentBehavior
        where TContext : ScenarioContext
    {
        public bool Done
        {
            get
            {
                exceptionInfo?.Throw();
                return isDone;
            }
        }

        public Task<ComponentRunner> CreateRunner(RunDescriptor run) => Task.FromResult<ComponentRunner>(new Runner(checkDone, () => isDone = true, info => exceptionInfo = info, (TContext)run.ScenarioContext));

        volatile ExceptionDispatchInfo exceptionInfo;
        volatile bool isDone;

        class Runner(
            Func<TContext, Task<bool>> isDone,
            Action setDone,
            Action<ExceptionDispatchInfo> setException,
            TContext scenarioContext)
            : ComponentRunner
        {
            public override string Name => "ServiceControlClient";

            public override Task Start(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public override Task ComponentsStarted(CancellationToken cancellationToken = default)
            {
                tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                checkTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!tokenSource.IsCancellationRequested)
                        {
                            if (await isDone(scenarioContext))
                            {
                                setDone();
                                return;
                            }

                            await Task.Delay(100, tokenSource.Token);
                        }
                    }
                    catch (Exception e)
                    {
                        setException(ExceptionDispatchInfo.Capture(e));
                    }
                }, tokenSource.Token);
                return Task.CompletedTask;
            }

            public override async Task Stop(CancellationToken cancellationToken = default)
            {
                if (checkTask == null)
                {
                    return;
                }

                await tokenSource.CancelAsync();
                try
                {
                    await checkTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Even though we are stopping, ONLY swallow when OCE from callee to not hide any ungraceful stop errors
                }
                finally
                {
                    tokenSource.Dispose();
                }
            }

            Task checkTask;
            CancellationTokenSource tokenSource;
        }
    }
}