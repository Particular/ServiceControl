namespace ServiceControl.UnitTests.HeartbeatMonitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using ServiceControl.HeartbeatMonitoring;
    using NUnit.Framework;

    public class When_endpoints_emit_heartbeats
    {
        [Test]
        public void Should_be_added_to_the_list_of_active_endpoint_instances()
        {
            var monitor = new HeartbeatMonitor(new FakeBus());

            monitor.RegisterHeartbeat("MyScaledOutEndpoint", "machineA", DateTime.UtcNow);
            monitor.RegisterHeartbeat("MyScaledOutEndpoint", "machineB", DateTime.UtcNow);
            monitor.RegisterHeartbeat("MyLostEndpoint", "machineA", DateTime.UtcNow - TimeSpan.FromHours(1));

            monitor.RefreshHeartbeatsStatuses(null);

            var knowEndpointInstances = monitor.HeartbeatStatuses;

            Assert.AreEqual(3, knowEndpointInstances.Count(), "Both endpoints should be registered");

            Assert.NotNull(knowEndpointInstances.SingleOrDefault(s => s.Endpoint == "MyScaledOutEndpoint" && s.Machine == "machineA"));
            Assert.NotNull(knowEndpointInstances.SingleOrDefault(s => s.Endpoint == "MyScaledOutEndpoint" && s.Machine == "machineB"));

            Assert.True(knowEndpointInstances.First(s => s.Endpoint == "MyScaledOutEndpoint").Active);


            Assert.False(knowEndpointInstances.Single(s => s.Endpoint == "MyLostEndpoint").Active);
        }
    }

    public class FakeBus : IBus
    {
        public T CreateInstance<T>()
        {
            throw new NotImplementedException();
        }

        public T CreateInstance<T>(Action<T> action)
        {
            throw new NotImplementedException();
        }

        public object CreateInstance(Type messageType)
        {
            throw new NotImplementedException();
        }

        public void Publish<T>(params T[] messages)
        {
        }

        public void Publish<T>(T message)
        {
        }

        public void Publish<T>()
        {
        }

        public void Publish<T>(Action<T> messageConstructor)
        {
        }

        public void Subscribe(Type messageType)
        {
        }

        public void Subscribe<T>()
        {
        }

        public void Subscribe(Type messageType, Predicate<object> condition)
        {
        }

        public void Subscribe<T>(Predicate<T> condition)
        {
        }

        public void Unsubscribe(Type messageType)
        {
        }

        public void Unsubscribe<T>()
        {
        }

        public ICallback SendLocal(params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback SendLocal(object message)
        {
            throw new NotImplementedException();
        }

        public ICallback SendLocal<T>(Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(string destination, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(string destination, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(Address address, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(Address address, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(string destination, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(Address address, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(string destination, string correlationId, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(string destination, string correlationId, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(Address address, string correlationId, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Send(Address address, string correlationId, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(string destination, string correlationId, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback Send<T>(Address address, string correlationId, Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public ICallback SendToSites(IEnumerable<string> siteKeys, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback SendToSites(IEnumerable<string> siteKeys, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Defer(TimeSpan delay, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Defer(TimeSpan delay, object message)
        {
            throw new NotImplementedException();
        }

        public ICallback Defer(DateTime processAt, params object[] messages)
        {
            throw new NotImplementedException();
        }

        public ICallback Defer(DateTime processAt, object message)
        {
            throw new NotImplementedException();
        }

        public void Reply(params object[] messages)
        {
            throw new NotImplementedException();
        }

        public void Reply(object message)
        {
            throw new NotImplementedException();
        }

        public void Reply<T>(Action<T> messageConstructor)
        {
            throw new NotImplementedException();
        }

        public void Return<T>(T errorEnum)
        {
            throw new NotImplementedException();
        }

        public void HandleCurrentMessageLater()
        {
            throw new NotImplementedException();
        }

        public void ForwardCurrentMessageTo(string destination)
        {
            throw new NotImplementedException();
        }

        public void DoNotContinueDispatchingCurrentMessageToHandlers()
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, string> OutgoingHeaders
        {
            get { throw new NotImplementedException(); }
        }

        public IMessageContext CurrentMessageContext
        {
            get { throw new NotImplementedException(); }
        }

        public IInMemoryOperations InMemory
        {
            get { throw new NotImplementedException(); }
        }
    }
}