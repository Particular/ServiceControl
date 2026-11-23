namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using ServiceControl.Migration.Guided;

[TestFixture]
class MigrationConsoleTests
{
    static (MigrationConsole Console, StringWriter Output) Scripted(string input, bool inputRedirected = false)
    {
        var output = new StringWriter();
        return (new MigrationConsole(new StringReader(input), output, inputRedirected, canRedraw: false), output);
    }

    [TestCase("y", true)]
    [TestCase(" Y ", true)]
    [TestCase("", false)]
    [TestCase("yes", true)]
    [TestCase("yep", false)]
    [TestCase("n", false)]
    public void Confirm_is_yes_only_for_y_and_yes_and_no_on_enter(string answer, bool expected)
    {
        var (console, output) = Scripted(answer + "\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(console.Confirm("Start the copy?", nonInteractive: false), Is.EqualTo(expected));
            Assert.That(output.ToString(), Does.StartWith("Start the copy? [y/N] "));
        }
    }

    [Test]
    public void Non_interactive_answers_yes_without_reading_and_says_so()
    {
        var (console, output) = Scripted("", inputRedirected: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(console.Confirm("Start the copy?", nonInteractive: true), Is.True);
            Assert.That(output.ToString(), Does.Contain("Start the copy? [y/N] y (--non-interactive)"));
        }
    }

    [TestCase("y\n", true)]
    [TestCase("", false)]
    public void A_prompt_that_cannot_be_answered_fails_naming_non_interactive_and_the_prompt(string input, bool inputRedirected) =>
        Assert.That(() => Scripted(input, inputRedirected).Console.Confirm("Is this correct?", nonInteractive: false),
            Throws.InvalidOperationException.With.Message.Contain("--non-interactive").And.Message.Contain("Is this correct?"));

    [Test]
    public void End_of_input_from_a_console_waits_for_a_cancel_that_arrives_after_it()
    {
        // Ctrl+C ends the read first, and the cancel handler runs a moment later on another thread.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.That(() => Scripted("").Console.Confirm("Is this correct?", nonInteractive: false, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void An_answer_read_after_a_cancel_is_a_cancellation()
    {
        // On Linux a Ctrl+C does not end the read, so the line typed after it comes back with the token already cancelled.
        Assert.That(() => Scripted("y\n").Console.Confirm("Is this correct?", nonInteractive: false, new CancellationToken(canceled: true)),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Choose_asks_again_until_it_gets_a_listed_key()
    {
        var (console, output) = Scripted("\nx\n C \n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(console.Choose("Choice:", "sca"), Is.EqualTo('c'));
            Assert.That(Regex.Matches(output.ToString(), "Choice:").Count, Is.EqualTo(3));
        }
    }

    [TestCase("ABANDON", true)]
    [TestCase(" ABANDON ", true)]
    [TestCase("abandon", false)]
    [TestCase("", false)]
    public void ConfirmTyped_needs_the_exact_word(string answer, bool expected) =>
        Assert.That(Scripted(answer + "\n").Console.ConfirmTyped("Type ABANDON to confirm:", "ABANDON"), Is.EqualTo(expected));
}
