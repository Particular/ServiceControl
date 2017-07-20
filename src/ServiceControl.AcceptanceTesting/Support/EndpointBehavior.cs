﻿namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;

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
        public WhenDefinition(Predicate<TContext> condition, Action<IBus> action)
        {
            Id = Guid.NewGuid();
            this.condition = condition;
            busAction = action;
        }

        public WhenDefinition(Predicate<TContext> condition, Action<IBus, TContext> actionWithContext)
        {
            Id = Guid.NewGuid();
            this.condition = condition;
            busAndContextAction = actionWithContext;
        }

        public Guid Id { get; }

        public bool ExecuteAction(ScenarioContext context, IBus bus)
        {
            var c = context as TContext;

            if (!condition(c))
            {
                return false;
            }


            if (busAction != null)
            {
                busAction(bus);
            }
            else
            {
                busAndContextAction(bus, c);
            }

            return true;
        }

        readonly Predicate<TContext> condition;
        readonly Action<IBus> busAction;
        readonly Action<IBus, TContext> busAndContextAction;
    }

    public interface IGivenDefinition
    {
        Action<IBus> GetAction(ScenarioContext context);
    }


    public interface IWhenDefinition
    {
        bool ExecuteAction(ScenarioContext context, IBus bus);

        Guid Id { get; }
    }
}