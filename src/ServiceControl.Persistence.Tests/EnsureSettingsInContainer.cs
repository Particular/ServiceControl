namespace ServiceControl.Persistence.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Persistence;

    public sealed class EnsureSettingsInContainer : PersistenceTestBase
    {
        [Test]
        public void CheckForBothTypes()
        {
            // Persistence implementation must register singleton as base type as some components need to inject that
            var baseSettings = ServiceProvider.GetRequiredService<PersistenceSettings>();

            var actualType = baseSettings.GetType();
            Assert.That(actualType, Is.Not.EqualTo(typeof(PersistenceSettings)));

            // Persistence implementation must also register the same singleton as the persister-specific type
            var settingsAsActualType = ServiceProvider.GetRequiredService(actualType);
            Assert.That(settingsAsActualType, Is.SameAs(baseSettings));
        }
    }
}