namespace ServiceBus.Management.AcceptanceTests
{
    using System;
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
            context.RegisterStartupTask(b => new DispatchTask(b.Build<IDispatchMessages>(), () => CreateMessage((TContext)b.Build<ScenarioContext>()), s => BeforeDispatch(s, (TContext)b.Build<ScenarioContext>()), s => AfterDispatch(s, (TContext)b.Build<ScenarioContext>())));
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
            private IDispatchMessages dispatchMessages;
            private Func<TransportOperations> operationFactory;
            private Func<IMessageSession, Task> before;
            private Func<IMessageSession, Task> after;

            public DispatchTask(IDispatchMessages dispatcher, Func<TransportOperations> operationFactory, Func<IMessageSession, Task> before, Func<IMessageSession, Task> after)
            {
                this.after = after;
                this.before = before;
                this.operationFactory = operationFactory;
                dispatchMessages = dispatcher;
            }
            
            protected override async Task OnStart(IMessageSession session)
            {
                await before(session).ConfigureAwait(false);
                await dispatchMessages.Dispatch(operationFactory(), new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
                await after(session).ConfigureAwait(false);                
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }
        }
    }
}