namespace ServiceControl.UnitTests.Migration;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class OptionalCategoryWindowsAreValidCheckTests
{
    static readonly TimeSpan EventRetention = TimeSpan.FromDays(3);
    static readonly TimeSpan ErrorRetention = TimeSpan.FromDays(11);

    [TearDown]
    public void ClearWindows()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ARCHIVEDANDRESOLVEDFAILEDMESSAGESWINDOW", null);
    }

    [Test]
    public async Task Unset_windows_pass_and_default_to_the_retention_periods_it_was_given()
    {
        var check = new OptionalCategoryWindowsAreValidCheck(EventRetention, ErrorRetention);

        await check.Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Options.EventLogWindow, Is.EqualTo(EventRetention));
            Assert.That(check.Options.ArchivedAndResolvedFailedMessagesWindow, Is.EqualTo(ErrorRetention));
        }
    }

    [Test]
    public void A_window_that_is_not_a_time_span_is_refused_and_named()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_ARCHIVEDANDRESOLVEDFAILEDMESSAGESWINDOW", "a week");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new OptionalCategoryWindowsAreValidCheck(EventRetention, ErrorRetention).Run());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain("a week"));
            Assert.That(exception.Message, Does.Contain(MigrationSettings.ArchivedAndResolvedFailedMessagesWindowKey));
        }
    }
}
