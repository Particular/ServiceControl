#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationCategoryRegistryTests
{
    [Test]
    public void Contains_all_eighteen_categories_with_unique_ids()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MigrationCategoryRegistry.All, Has.Count.EqualTo(18));
            Assert.That(MigrationCategoryRegistry.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(18));
        }
    }

    [Test]
    public void Required_categories_are_ordered_exactly_as_the_contract_lists_them()
    {
        var requiredIds = MigrationCategoryRegistry.All
            .Where(c => c.Kind == MigrationCategoryKind.Required)
            .OrderBy(c => c.Order)
            .Select(c => c.Id)
            .ToArray();

        Assert.That(requiredIds, Is.EqualTo(new[]
        {
            "KnownEndpoints", "EndpointSettings", "MessageRedirects", "Subscriptions",
            "NotificationSettings", "TrialEndDate", "RetryOperations",
            "LicensingEndpoints", "LicensingThroughput", "LicensingReportMasks",
            "LicensedEndpointDetails", "UnresolvedAndRetryIssuedFailedMessages"
        }));
    }

    [Test]
    public void Optional_categories_are_ordered_exactly_as_the_contract_lists_them()
    {
        var optionalIds = MigrationCategoryRegistry.All
            .Where(c => c.Kind == MigrationCategoryKind.Optional)
            .OrderBy(c => c.Order)
            .Select(c => c.Id)
            .ToArray();

        Assert.That(optionalIds, Is.EqualTo(new[]
        {
            "EventLog", "CustomChecks", "FailedErrorImports",
            "FailedMessageEdits", "ArchivedAndResolvedFailedMessages", "GroupComments"
        }));
    }

    [Test]
    public void Three_categories_declare_a_MustFollow()
    {
        var declared = MigrationCategoryRegistry.All
            .Where(c => c.MustFollow is not null)
            .ToDictionary(c => c.Id, c => c.MustFollow);

        Assert.That(declared, Is.EqualTo(new Dictionary<string, string?>
        {
            // A cascading foreign key: throughput rows cannot exist before their endpoint row.
            ["LicensingThroughput"] = "LicensingEndpoints",
            // Keeps settings back while known endpoints are unfinished. The heartbeat sync is kept off
            // the copied settings because both categories finish before the host opens.
            ["EndpointSettings"] = "KnownEndpoints",
            // A group comment whose failed messages have not arrived reads as an orphan to the
            // retention sweeper, which deletes it.
            ["GroupComments"] = "ArchivedAndResolvedFailedMessages"
        }));
    }

    [Test]
    public void The_two_failed_message_categories_and_failed_imports_carry_bodies()
    {
        var carriesBodies = MigrationCategoryRegistry.All.Where(c => c.CarriesBodies).Select(c => c.Id).ToArray();

        Assert.That(carriesBodies, Is.EquivalentTo(new[] { "UnresolvedAndRetryIssuedFailedMessages", "ArchivedAndResolvedFailedMessages", "FailedErrorImports" }));
    }

    [Test]
    public void The_two_failed_message_categories_split_every_status_between_them()
    {
        var split = MigrationCategoryRegistry.UnresolvedAndRetryIssuedStatuses.Concat(MigrationCategoryRegistry.ArchivedAndResolvedStatuses).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(split, Is.Unique);
            Assert.That(split, Is.EquivalentTo(Enum.GetValues<FailedMessageStatus>()),
                "a status neither category claims is a failed message no migration copies");
        }
    }

    [Test]
    public void Find_returns_null_for_an_unknown_id()
    {
        Assert.That(MigrationCategoryRegistry.Find("NoSuchCategory"), Is.Null);
    }

    [Test]
    public void Find_returns_the_category_for_a_known_id()
    {
        Assert.That(MigrationCategoryRegistry.Find("EventLog")?.Kind, Is.EqualTo(MigrationCategoryKind.Optional));
    }
}
