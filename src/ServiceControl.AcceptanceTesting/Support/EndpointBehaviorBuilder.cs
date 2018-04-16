namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class EndpointBehaviorBuilder<TContext> where TContext : ScenarioContext
    {

        public EndpointBehaviorBuilder(Type type)
        {
            behavior = new EndpointBehavior(type)
            {
                Givens = new List<IGivenDefinition>(),
                Whens = new List<IWhenDefinition>()
            };
        }

        public EndpointBehaviorBuilder<TContext> Given(Action<IBus> action)
        {
            behavior.Givens.Add(new GivenDefinition<TContext>(action));

            return this;
        }


        public EndpointBehaviorBuilder<TContext> Given(Action<IBus, TContext> action)
        {
            behavior.Givens.Add(new GivenDefinition<TContext>(action));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Action<IBus> action)
        {
            return When(c => true, action);
        }

        public EndpointBehaviorBuilder<TContext> When(Func<IBus, Task> action)
        {
            return When(c => true, action);
        }

        public EndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Action<IBus> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), bus =>
            {
                action(bus);
                return Task.FromResult(0);
            }));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Action<IBus> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(condition, bus =>
            {
                action(bus);
                return Task.FromResult(0);
            }));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Func<IBus, Task> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), action));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Action<IBus, TContext> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(condition, (bus, ctx) =>
            {
                action(bus, ctx);
                return Task.FromResult(0);
            }));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Func<TContext, Task<bool>> condition, Func<IBus, TContext, Task> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(condition, action));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Action<IBus, TContext> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), (bus, ctx) =>
            {
                action(bus, ctx);
                return Task.FromResult(0);
            }));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> When(Predicate<TContext> condition, Func<IBus, TContext, Task> action)
        {
            behavior.Whens.Add(new WhenDefinition<TContext>(ctx => Task.FromResult(condition(ctx)), action));

            return this;
        }

        public EndpointBehaviorBuilder<TContext> CustomConfig(Action<BusConfiguration> action)
        {
            behavior.CustomConfig.Add(action);

            return this;
        }

        public EndpointBehavior Build()
        {
            return behavior;
        }

        readonly EndpointBehavior behavior;
    }
}