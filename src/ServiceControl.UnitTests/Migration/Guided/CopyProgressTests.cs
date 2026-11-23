namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class CopyProgressTests
{
    [Test]
    public void Every_line_is_79_columns_with_the_state_at_column_70_so_a_redraw_covers_the_last()
    {
        var states = Enum.GetValues<MigrationCategoryState>().Select(state => state.CustomerState()).Append("Waiting").Distinct();

        var lines =
            from category in MigrationCategoryRegistry.All
            from state in states
            from total in new long?[] { 1_234_567, null }
            select (State: state, Text: CopyProgress.Line(category.Id, 1_234_567, total, state));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines.Select(line => (line.Text.Length, line.Text[70..].TrimEnd())), Is.EqualTo(lines.Select(line => (79, line.State))));
            Assert.That(CopyProgress.Line(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, 1_234_567, 1_234_567, "Abandoned"),
                Does.Contain("1,234,567 / 1,234,567").And.Contain("100%").And.Contain("Abandoned"));
        }
    }

    [Test]
    public void A_redrawn_line_no_finish_ended_is_ended_before_other_text()
    {
        var output = new StringWriter();
        var progress = new CopyProgress(new MigrationConsole(TextReader.Null, output, inputRedirected: false, canRedraw: true));

        progress.Update(Copying(1, total: 100));
        progress.EndOpenLine();
        progress.Finish(Copying(98, total: 100) with { SkippedCount = 1, AlreadyPresentCount = 1, State = MigrationCategoryState.Complete });
        progress.EndOpenLine();

        var copying = CopyProgress.Line(MigrationCategoryIds.KnownEndpoints, 1, 100, "Copying");
        var done = CopyProgress.Line(MigrationCategoryIds.KnownEndpoints, 100, 100, "Done");
        Assert.That(output.ToString(), Is.EqualTo($"\r{copying}{Environment.NewLine}\r{done}{Environment.NewLine}"),
            "a line Finish ended gets no second newline, and its count includes the skipped and already present rows");
    }

    [Test]
    public void Redirected_output_prints_a_line_at_each_ten_percent_and_at_the_end()
    {
        var output = new StringWriter();
        var progress = new CopyProgress(new MigrationConsole(TextReader.Null, output, inputRedirected: true, canRedraw: false));

        foreach (var copied in new long[] { 0, 5, 10, 19, 20, 100 })
        {
            progress.Update(Copying(copied, total: 100));
        }
        progress.Finish(Copying(100, total: 100) with { State = MigrationCategoryState.Complete });

        Assert.That(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(text => Regex.Match(text, @"\d+%").Value),
            Is.EqualTo(new[] { "10%", "20%", "100%", "100%" }));
    }

    [Test]
    public async Task Each_copy_pass_lists_only_the_fault_skips_it_made()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"), GuidedMigrationHarness.Row("KnownEndpoints/2"));
        harness.Target.RejectKey("KnownEndpoints/2", MigrationSkipReason.RequiredValueMissing);
        harness.Source.Seed(MigrationCategoryIds.GroupComments, GuidedMigrationHarness.Row("GroupComment/1"));
        harness.Target.RejectKey("GroupComment/1", MigrationSkipReason.BlankGroupComment);

        Assert.That(async () => await harness.Run(GuidedMigrationArguments.Unattended with { Retry = new HashSet<string> { MigrationCategoryIds.KnownEndpoints } }),
            Throws.Exception, "the retry skips the same row, so the category is still Failed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Regex.Matches(harness.Output, @"(?m)^Skipped rows\r?$"), Has.Count.EqualTo(2), "the first copy and the retry each skipped the row");
            Assert.That(Regex.Matches(harness.Output, @"(?m)^   KnownEndpoints\s+KnownEndpoints/2  RequiredValueMissing\r?$"), Has.Count.EqualTo(2), "each pass lists its own skip once");
            Assert.That(Regex.Matches(harness.Output, @"(?m)^   GroupComments\s+BlankGroupComment  1, harmless\r?$"), Has.Count.EqualTo(1), "the retry copies only KnownEndpoints, so it adds no harmless skip");
        }
    }

    [Test]
    public async Task A_failed_progress_read_does_not_fail_the_copy()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.MessageRedirects, [.. Enumerable.Range(1, 6).Select(number => GuidedMigrationHarness.Row($"MessageRedirects/{number}"))]);
        harness.Source.Seed(MigrationCategoryIds.GroupComments, GuidedMigrationHarness.Row("GroupComment/1"));
        harness.Target.RejectKey("GroupComment/1", MigrationSkipReason.BlankGroupComment);
        var polled = false;

        // The poll waits for its next second only after it handles a read, so the clock moves a second at a time until it reads.
        bool AdvanceUntilTheNextRead()
        {
            var reads = harness.Store.ReadAllCount;

            for (var second = 0; second < 20; second++)
            {
                harness.Clock.Advance(TimeSpan.FromSeconds(1));
                if (SpinWait.SpinUntil(() => harness.Store.ReadAllCount > reads, TimeSpan.FromMilliseconds(500)))
                {
                    return true;
                }
            }

            return false;
        }

        harness.Target.BeforeWrite = checkpoint =>
        {
            if (checkpoint.CategoryId == MigrationCategoryIds.MessageRedirects && checkpoint.CopiedCount == 3)
            {
                harness.Store.ReadFailure = new InvalidOperationException("The checkpoint store did not answer");
                var failed = AdvanceUntilTheNextRead();
                harness.Store.ReadFailure = null;
                polled = failed && AdvanceUntilTheNextRead();
            }
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await harness.Run(GuidedMigrationArguments.Unattended), Is.Zero);

            var copyStep = harness.Output[harness.Output.IndexOf("Copying required data", StringComparison.Ordinal)..];
            var shown = Regex.Matches(copyStep, @"(?m)^   (\S+)\s+(\d+) / \S+\s+(\d+%)  (\S+)").Select(match => $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value} {match.Groups[4].Value}");

            Assert.That(polled, Is.True, "the poll did not read again after its failed read");
            Assert.That(shown.Take(5), Is.EqualTo(new[] { "KnownEndpoints 0 100% Done", "EndpointSettings 0 100% Done", "MessageRedirects 3 50% Copying", "MessageRedirects 6 100% Done", "Subscriptions 0 100% Done" }));
            Assert.That(harness.Output, Does.Match(@"(?m)^   MessageRedirects\s+3 / 6\s+50%  Copying"), "the poll keeps going after a failed read");
            Assert.That(harness.Output, Does.Match(@"(?m)^   MessageRedirects\s+6 / 6\s+100%  Done"));
            Assert.That(harness.Output, Does.Match(@"(?m)^   GroupComments\s+BlankGroupComment  1, harmless\r?$"));
        }
    }

    [Test]
    public async Task A_copy_that_throws_still_lists_the_rows_it_skipped()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Seed(MigrationCategoryIds.KnownEndpoints, GuidedMigrationHarness.Row("KnownEndpoints/1"), GuidedMigrationHarness.Row("KnownEndpoints/2"));
        harness.Target.RejectKey("KnownEndpoints/2", MigrationSkipReason.RequiredValueMissing);
        harness.Source.Seed(MigrationCategoryIds.MessageRedirects, GuidedMigrationHarness.Row("MessageRedirects/1"));
        harness.Target.FailOnCallNumber = 2;
        harness.Target.FailWith = new MigrationCheckpointConflictException("Another writer saved MessageRedirects.");

        Assert.That(async () => await harness.Run(GuidedMigrationArguments.Unattended),
            Throws.Exception.With.Message.Contain("another writer saved a migration checkpoint"));
        Assert.That(harness.Output, Does.Match(@"(?m)^Skipped rows\r?\n   KnownEndpoints\s+KnownEndpoints/2  RequiredValueMissing\r?$"), "the skip was committed before the copy failed");
    }

    [Test]
    public void A_category_blocked_behind_a_failed_one_ends_as_waiting_not_copying()
    {
        var output = new StringWriter();
        var progress = new CopyProgress(new MigrationConsole(TextReader.Null, output, inputRedirected: true, canRedraw: false));

        progress.Finish(new MigrationCheckpoint(MigrationCategoryIds.GroupComments, MigrationCategoryState.Blocked, null, 0, 0, 3, null, null, null, null, null));

        Assert.That(output.ToString().TrimEnd(), Is.EqualTo(CopyProgress.Line(MigrationCategoryIds.GroupComments, 0, 3, "Waiting").TrimEnd()));
    }

    static MigrationCheckpoint Copying(long copied, long total) =>
        new(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress, null, copied, 0, total, null, null, null, null, null);
}
