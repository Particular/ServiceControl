namespace ServiceControl.UnitTests
{
    using System.Linq;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;

    [TestFixture]
    class EventHierarchy
    {
        [TestCase]
        public void EnsureEventHierarchyIsFlat()
        {
            var serviceControlAssembly = typeof(Settings).Assembly;
            var eventTypes = serviceControlAssembly.GetTypes().Where(typeof(IEvent).IsAssignableFrom).Where(x => !x.IsAbstract).ToArray();

            var flatEvents = eventTypes.Where(t => t.BaseType == typeof(object)).ToArray();

            var nonFlatEvents = eventTypes.Except(flatEvents).ToArray();

            Assert.That(eventTypes, Is.Not.Empty);
            Assert.That(nonFlatEvents, Is.Empty, "Complex Event Hierarchy causes duplicate event handling with Azure ServiceBus and SubscribeToOwnEvents");
        }
    }
}