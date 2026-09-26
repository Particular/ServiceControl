namespace ServiceControl.Audit.Persistence.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixture]
    class AuditDataStoreRegistrationTests : PersistenceTestFixture
    {
        [Test]
        public void Should_resolve_all_capabilities_from_one_store_instance()
        {
            var messagesViewStore = ServiceProvider.GetRequiredService<IAuditMessagesViewDataStore>();
            var sagaHistoryStore = ServiceProvider.GetRequiredService<ISagaHistoryDataStore>();

            Assert.That(sagaHistoryStore, Is.SameAs(messagesViewStore));
        }
    }
}