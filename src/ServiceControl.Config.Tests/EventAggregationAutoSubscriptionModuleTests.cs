﻿namespace ServiceControl.Config.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Caliburn.Micro;
    using Framework.Modules;
    using NUnit.Framework;

    [TestFixture]
    public class EventAggregationAutoSubscriptionModuleTests
    {
        [Test]
        public void AutoSubscribesOnResolve()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new EventAggregationAutoSubscriptionModule());
            builder.RegisterType<FakeEventAggregator>().As<IEventAggregator>().AsSelf().SingleInstance();
            builder.RegisterType<FakeEventHandler>();
            builder.RegisterType<FakeNonEventHandler>();

            var container = builder.Build();

            var events = container.Resolve<FakeEventAggregator>();

            Assert.IsEmpty(events.Subscribers, "There should be no handlers until they are resolved");

            var handler = container.Resolve<FakeEventHandler>();

            Assert.Contains(handler, events.Subscribers, "Items should be subscribed on activation");

            var nonHandler = container.Resolve<FakeNonEventHandler>();

            Assert.That(events.Subscribers, Does.Not.Contain(nonHandler), "Non-handlers should not be subscribed on activation");
        }

        class FakeEvent { }

        class FakeEventHandler : IHandle<FakeEvent>
        {
            public Task HandleAsync(FakeEvent message, CancellationToken cancellationToken) => throw new NotImplementedException();
        }

        class FakeNonEventHandler
        {

        }

        class FakeEventAggregator : IEventAggregator
        {
            public List<object> Subscribers = [];

            public bool HandlerExistsFor(Type messageType) => throw new NotImplementedException();

            public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) => Subscribers.Add(subscriber);

            public void Unsubscribe(object subscriber) => throw new NotImplementedException();

            public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();
        }
    }
}