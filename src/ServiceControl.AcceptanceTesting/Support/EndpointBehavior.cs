namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class EndpointBehavior
    {
        public EndpointBehavior(Type builderType)
        {
            EndpointBuilderType = builderType;
            CustomConfig = new List<Action<BusConfiguration>>();
        }

        public Type EndpointBuilderType { get; }

        public List<IGivenDefinition> Givens { get; set; }
        public List<IWhenDefinition> Whens { get; set; }

        public List<Action<BusConfiguration>> CustomConfig { get; set; }
    }

    public class WhenDefinition<TContext> : IWhenDefinition where TContext : ScenarioContext
    {
        public WhenDefinition(Func<TContext, Task<bool>> condition, Func<IBus, Task> action)
        {
            Id = Guid.NewGuid();
            this.condition = condition;
            busAction = action;
        }

        public WhenDefinition(Func<TContext, Task<bool>> condition, Func<IBus, TContext, Task> actionWithContext)
        {
            Id = Guid.NewGuid();
            this.condition = condition;
            busAndContextAction = actionWithContext;
        }

        public Guid Id { get; }

        public async Task<bool> ExecuteAction(ScenarioContext context, IBus bus)
        {
            var c = context as TContext;

            if (!await condition(c))
            {
                return false;
            }


            if (busAction != null)
            {
                await busAction(bus).ConfigureAwait(false);
            }
            else
            {
                await busAndContextAction(bus, c).ConfigureAwait(false);
            }

            return true;
        }

        readonly Func<TContext, Task<bool>> condition;
        readonly Func<IBus, Task> busAction;
        readonly Func<IBus, TContext, Task> busAndContextAction;
    }

    public interface IGivenDefinition
    {
        Action<IBus> GetAction(ScenarioContext context);
    }


    public interface IWhenDefinition
    {
        Task<bool> ExecuteAction(ScenarioContext context, IBus bus);

        Guid Id { get; }
    }
}