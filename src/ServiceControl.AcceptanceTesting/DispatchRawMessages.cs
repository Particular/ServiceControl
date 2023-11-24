namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Extensibility;
    using NServiceBus.Features;
    using NServiceBus.Transport;

    public abstract class DispatchRawMessages<TContext> : Feature
        where TContext : ScenarioContext
    {
        protected DispatchRawMessages()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => new DispatchTask(
                b.Build<IMessageDispatcher>(),
                () => CreateMessage((TContext)b.Build<ScenarioContext>()),
                s => BeforeDispatch(s, (TContext)b.Build<ScenarioContext>()),
                s => AfterDispatch(s, (TContext)b.Build<ScenarioContext>()),
                b.Build<ScenarioContext>()));
        }

        protected abstract TransportOperations CreateMessage(TContext context);

        protected virtual Task BeforeDispatch(IMessageSession session, TContext context)
        {
            return Task.FromResult(0);
        }

        protected virtual Task AfterDispatch(IMessageSession session, TContext context)
        {
            return Task.FromResult(0);
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