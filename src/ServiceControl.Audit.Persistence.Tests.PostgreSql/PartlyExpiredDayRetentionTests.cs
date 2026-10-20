namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    class PartlyExpiredDayRetentionTests : EFPersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = settings =>
            {
                settings.AuditRetentionPeriod = TimeSpan.FromDays(1);
                // FakeTimeProvider only moves forward; tomorrow's midday is independent of the current hour.
                configuration.TimeProvider.SetUtcNow(new DateTimeOffset(configuration.TimeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(12), TimeSpan.Zero));
            };
            return base.Setup();
        }

        [Test]
        public async Task Keeps_the_body_and_row_until_the_whole_day_has_expired()
        {
            await Ingest(MakeMessage(contentType: "application/octet-stream"), [1, 2, 3]);
            var retention = configuration.CreateRetention();

            // The ingestion hour has expired but the rest of its day has not
            configuration.TimeProvider.Advance(TimeSpan.FromHours(25));
            await retention.SweepNow();
            var partlyExpired = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));
            var filesWhilePartlyExpired = Directory.GetFiles(configuration.BodyStoragePath);

            configuration.TimeProvider.Advance(TimeSpan.FromHours(12));
            await retention.SweepNow();
            var expired = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(partlyExpired.Results, Has.Count.EqualTo(1));
                Assert.That(filesWhilePartlyExpired, Has.Length.EqualTo(1));
                Assert.That(expired.Results, Is.Empty);
                Assert.That(Directory.GetFiles(configuration.BodyStoragePath), Is.Empty);
            }
        }
    }
}
