namespace ServiceControl.Config.Tests
{
    using System;
    using System.Linq;
    using Caliburn.Micro;
    using NUnit.Framework;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.UI.ListInstances;

    class PubSubAssumptions
    {
        [Test]
        public void OnlyOneRefreshSubscriber()
        {
            var refreshHandlers = GetHandlers(typeof(RefreshInstances));
            var postRefreshHandlers = GetHandlers(typeof(PostRefreshInstances));

            Assert.That(refreshHandlers.Single(), Is.EqualTo(typeof(ListInstancesViewModel)), $"{nameof(RefreshInstances)} can only have one subscriber: {nameof(ListInstancesViewModel)}. Everything else must subscribe to {nameof(PostRefreshInstances)}.");
            Assert.That(postRefreshHandlers.Count, Is.GreaterThan(1));
        }

        Type[] GetHandlers(Type eventType)
        {
            var handlerTypeInterface = typeof(IHandle<>).MakeGenericType(eventType);

            return eventType.Assembly.GetTypes()
                .Where(t => t.GetInterfaces().Contains(handlerTypeInterface))
                .ToArray();
        }
    }
}