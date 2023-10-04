namespace ServiceControl.PersistenceTests
{
    using NUnit.Framework;
    using ServiceControl.Persistence;

    public sealed class EnsureSettingsInContainer : PersistenceTestBase
    {
        [Test]
        public void CheckForBothTypes()
        {
            // Persistence implementation must register singleton as base type as some compoennts need to inject that
            var baseSettings = GetRequiredService<PersistenceSettings>();

            var actualType = baseSettings.GetType();
            Assert.AreNotEqual(actualType, typeof(PersistenceSettings));

            // Persistence implementation must also register the same singleton as the persister-specific type
            var settingsAsActualType = GetRequiredService(actualType);
            Assert.AreSame(baseSettings, settingsAsActualType);
        }
    }
}