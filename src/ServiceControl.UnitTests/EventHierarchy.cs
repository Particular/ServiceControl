namespace ServiceControl.UnitTests
{
    using System.Linq;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl;

    [TestFixture]
    class EventHierarchy
    {
        [TestCase]
        public void EnsureEventHierarchyIsFlat()
        {
            var serviceControlAssembly = typeof(Bootstrapper).Assembly;
            var eventTypes = serviceControlAssembly.GetTypes().Where(typeof(IEvent).IsAssignableFrom).ToArray();

            var flatEvents = eventTypes.Where(t => t.BaseType == typeof(object)).ToArray();

            var nonFlatEvents = eventTypes.Except(flatEvents).ToArray();

            Assert.IsEmpty(nonFlatEvents, "Complex Event Hierarchy causes duplicate event handling with Azure ServiceBus and SubscribeToOwnEvents");
        }
    }
}
