namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;

    class EFNoBodyStorageTests : EFPersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = settings =>
            {
                settings.MaxBodySizeToStore = MaxBodySizeToStore;
                settings.AuditRetentionPeriod = TimeSpan.FromDays(1);
                settings.PersisterSpecificSettings.Remove(BodyStorageSettingsParser.StorageTypeKey);
                settings.PersisterSpecificSettings.Remove(BodyStorageSettingsParser.FileSystemStoragePathKey);
            };
            return base.Setup();
        }

        [Test]
        public async Task An_oversized_body_is_dropped_but_stays_searchable()
        {
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes("findme " + new string('x', MaxBodySizeToStore)));

            var bodyUrl = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single().BodyUrl;
            var body = await MessagesViewStore.GetMessageBody(bodyUrl.Split('/')[2], TestContext.CurrentContext.CancellationToken);
            var found = await MessagesViewStore.QueryMessages("findme", new PagingInfo(), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(body.Found, Is.True);
                Assert.That(body.HasContent, Is.False);
                Assert.That(found.Results, Has.Count.EqualTo(1));
                Assert.That(Directory.GetFiles(configuration.BodyStoragePath), Is.Empty);
            }
        }

        [Test]
        public async Task Retention_sweeps_without_a_body_store()
        {
            await Ingest(MakeMessage(), Encoding.UTF8.GetBytes(new string('x', MaxBodySizeToStore + 1)));

            configuration.TimeProvider.Advance(TimeSpan.FromDays(3));
            await configuration.CreateRetention().SweepNow();

            var remaining = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));
            Assert.That(remaining.Results, Is.Empty);
        }

        const int MaxBodySizeToStore = 1000;
    }
}
