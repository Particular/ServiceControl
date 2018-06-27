namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class StateMachine<TContext, TState>
        where TContext : IStateMachineContext<TState>
    {
        Dictionary<TState, Func<TContext, Task<TState>>> handlers = new Dictionary<TState, Func<TContext, Task<TState>>>();

        public StateMachine<TContext, TState> When(TState value, Func<TContext, Task<TState>> handler)
        {
            handlers[value] = handler;
            return this;
        }

        public async Task<TState> Step(TContext context)
        {
            Func<TContext, Task<TState>> handler;
            var currentState = context.State;
            if (!handlers.TryGetValue(currentState, out handler))
            {
                throw new Exception("State machine has finished execution.");
            }

            var newState = await handler(context).ConfigureAwait(false);
            if (!newState.Equals(currentState))
            {
                Console.WriteLine($"Switching from {currentState} to {newState}");
            }
            context.State = newState;
            return newState;
        }
    }
}