namespace ServiceControl.UnitTests.Infrastructure;

using System;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Ingestion;

[TestFixture]
public class IngestionCountersTests
{
    [Test]
    public void Batches_add_their_messages_and_their_time()
    {
        var counters = new IngestionCounters(new FakeTimeProvider());

        counters.RecordBatch(10, TimeSpan.FromSeconds(2));
        counters.RecordBatch(5, TimeSpan.FromSeconds(1));

        var snapshot = counters.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Messages, Is.EqualTo(15));
            Assert.That(snapshot.BusySeconds, Is.EqualTo(3).Within(0.001));
        }
    }

    [Test]
    public void A_failed_batch_costs_time_but_stores_no_messages()
    {
        var counters = new IngestionCounters(new FakeTimeProvider());

        counters.RecordBatch(0, TimeSpan.FromSeconds(2));

        var snapshot = counters.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Messages, Is.EqualTo(0));
            Assert.That(snapshot.BusySeconds, Is.EqualTo(2).Within(0.001));
        }
    }

    [Test]
    public void Lag_lands_in_every_bucket_it_exceeds()
    {
        var counters = new IngestionCounters(new FakeTimeProvider());

        counters.RecordLag(TimeSpan.FromSeconds(30));
        counters.RecordLag(TimeSpan.FromMinutes(5));
        counters.RecordLag(TimeSpan.FromMinutes(30));
        counters.RecordLag(TimeSpan.FromHours(2));

        var snapshot = counters.GetSnapshot();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.LagKnownMessages, Is.EqualTo(4));
            Assert.That(snapshot.LagOverOneMinuteMessages, Is.EqualTo(3));
            Assert.That(snapshot.LagOverTenMinutesMessages, Is.EqualTo(2));
            Assert.That(snapshot.LagOverSixtyMinutesMessages, Is.EqualTo(1));
        }
    }

    [Test]
    public void Storage_time_accumulates()
    {
        var counters = new IngestionCounters(new FakeTimeProvider());

        counters.RecordStorage(TimeSpan.FromMilliseconds(500));
        counters.RecordStorage(TimeSpan.FromMilliseconds(250));

        Assert.That(counters.GetSnapshot().StorageSeconds, Is.EqualTo(0.75).Within(0.001));
    }
}
