namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

class HealthEnvironmentDataTests : PersistenceTestBase
{
    [Test]
    public async Task An_empty_schema_is_healthy()
    {
        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data, Does.ContainKey("Health.Error.FailedImports").WithValue("0"));
            Assert.That(data, Does.ContainKey("Health.Error.RetentionBehindHours").WithValue("0"));
            Assert.That(data, Does.ContainKey("Storage.UnresolvedFailedMessages").WithValue("0"));
        }
    }

    [Test]
    public async Task An_old_unresolved_message_does_not_put_retention_behind()
    {
        var ancient = DateTime.UtcNow.AddDays(-400);

        await Store(FailedMessage(FailedMessageStatus.Unresolved, ancient));

        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data, Does.ContainKey("Health.Error.RetentionBehindHours").WithValue("0"));
            Assert.That(data, Does.ContainKey("Storage.UnresolvedFailedMessages").WithValue("1"));
        }
    }

    [Test]
    public async Task Retention_is_behind_by_the_oldest_resolved_or_archived_message_past_the_window()
    {
        var retention = PersistenceTestsContext.DefaultRetentionPeriod;

        await Store(
            FailedMessage(FailedMessageStatus.Unresolved, DateTime.UtcNow.AddDays(-400)),
            FailedMessage(FailedMessageStatus.Resolved, DateTime.UtcNow - retention - TimeSpan.FromHours(48)),
            FailedMessage(FailedMessageStatus.Archived, DateTime.UtcNow - retention - TimeSpan.FromHours(24)));

        var data = await GetData();

        Assert.That(data, Does.ContainKey("Health.Error.RetentionBehindHours").WithValue("48"));
    }

    static FailedMessageEntity FailedMessage(FailedMessageStatus status, DateTime statusChangedAt) => new()
    {
        UniqueMessageId = Guid.NewGuid(),
        Status = status,
        StatusChangedAt = statusChangedAt,
        LastModified = statusChangedAt,
        NumberOfProcessingAttempts = 1,
        FirstTimeOfFailure = statusChangedAt,
        LastTimeOfFailure = statusChangedAt,
        LastAttemptedAt = statusChangedAt,
        HeadersJson = "{}",
        FailingEndpointAddress = "Shipping"
    };

    async Task Store(params FailedMessageEntity[] messages)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        dbContext.Set<FailedMessageEntity>().AddRange(messages);

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    async Task<Dictionary<string, string>> GetData()
    {
        var provider = ServiceProvider.GetRequiredService<IEnvironmentDataProvider>();
        var data = new Dictionary<string, string>();

        foreach (var datum in provider.GetData())
        {
            data[datum.Key] = await datum.ReadValue(CancellationToken.None);
        }

        return data;
    }
}
