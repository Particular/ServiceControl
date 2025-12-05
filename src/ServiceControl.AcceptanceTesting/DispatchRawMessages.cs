namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Transport;

    public abstract class DispatchRawMessages<TContext> : Feature
        where TContext : ScenarioContext
    {
        protected DispatchRawMessages()
        {
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(provider => new DispatchTask(
                provider.GetRequiredService<IMessageDispatcher>(),
                () => CreateMessage((TContext)provider.GetRequiredService<ScenarioContext>()),
                s => BeforeDispatch(s, (TContext)provider.GetRequiredService<ScenarioContext>()),
                s => AfterDispatch(s, (TContext)provider.GetRequiredService<ScenarioContext>()),
                provider.GetRequiredService<ScenarioContext>()));
        }

        protected abstract TransportOperations CreateMessage(TContext context);

        protected virtual Task BeforeDispatch(IMessageSession session, TContext context)
        {
            return Task.CompletedTask;
        }

        protected virtual Task AfterDispatch(IMessageSession session, TContext context)
        {
            return Task.CompletedTask;
        }

        class DispatchTask : FeatureStartupTask
        {
            public DispatchTask(IMessageDispatcher dispatcher, Func<TransportOperations> operationFactory, Func<IMessageSession, Task> before, Func<IMessageSession, Task> after, ScenarioContext scenarioContext)
            {
                this.after = after;
                this.scenarioContext = scenarioContext;
                this.before = before;
                this.operationFactory = operationFactory;
                dispatchMessages = dispatcher;
            }

            protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                await before(session);
                var operations = operationFactory();
                foreach (var op in operations.UnicastTransportOperations)
                {
                    op.Message.Headers["SC.SessionID"] = scenarioContext.TestRunId.ToString();
                }

                foreach (var op in operations.MulticastTransportOperations)
                {
                    op.Message.Headers["SC.SessionID"] = scenarioContext.TestRunId.ToString();
                }

                await dispatchMessages.Dispatch(operations, new TransportTransaction(), cancellationToken);
                await after(session);
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            IMessageDispatcher dispatchMessages;
            Func<TransportOperations> operationFactory;
            Func<IMessageSession, Task> before;
            Func<IMessageSession, Task> after;
            ScenarioContext scenarioContext;
        }
    }
}