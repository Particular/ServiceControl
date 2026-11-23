namespace ServiceControl.Migration.Guided;

using System;
using System.IO;
using System.Threading;
using ServiceControl.Configuration;

/// <summary>
/// All the output and prompts of the guided migration. A prompt with no console input to answer it fails instead of waiting.
/// </summary>
sealed class MigrationConsole(TextReader input, TextWriter output, bool inputRedirected, bool canRedraw)
{
    public static MigrationConsole System() =>
        new(Console.In, Console.Out, Console.IsInputRedirected, canRedraw: !Console.IsOutputRedirected && !AppEnvironment.RunningInContainer);

    static readonly TimeSpan CancelKeyGrace = TimeSpan.FromSeconds(2);

    public bool CanRedraw { get; } = canRedraw;

    public void WriteLine(string line = "") => output.WriteLine(line);

    public void Write(string text) => output.Write(text);

    public bool Confirm(string question, bool nonInteractive, CancellationToken cancellationToken = default)
    {
        if (nonInteractive)
        {
            output.WriteLine($"{question} [y/N] y (--non-interactive)");
            return true;
        }

        var answer = Ask(question, " [y/N] ", cancellationToken).Trim();
        return answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public char Choose(string prompt, string keys, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var answer = Ask(prompt, " ", cancellationToken).Trim();
            if (answer.Length > 0)
            {
                var key = char.ToLowerInvariant(answer[0]);
                if (keys.Contains(key))
                {
                    return key;
                }
            }
        }
    }

    public bool ConfirmTyped(string prompt, string word, CancellationToken cancellationToken = default) =>
        string.Equals(Ask(prompt, " ", cancellationToken).Trim(), word, StringComparison.Ordinal);

    string Ask(string prompt, string hint, CancellationToken cancellationToken)
    {
        output.Write(prompt + hint);

        if (inputRedirected)
        {
            throw NeedsConsole(prompt);
        }

        var line = input.ReadLine();
        if (line is null)
        {
            // Ctrl+C ends the read before the cancel handler, which runs on another thread, has cancelled the token.
            cancellationToken.WaitHandle.WaitOne(CancelKeyGrace);
            cancellationToken.ThrowIfCancellationRequested();
            throw NeedsConsole(prompt);
        }

        // On Linux a Ctrl+C does not end the read, so the answer typed after it must not count.
        cancellationToken.ThrowIfCancellationRequested();
        return line;
    }

    static InvalidOperationException NeedsConsole(string prompt) =>
        new($"--start-data-migration needs a console to answer \"{prompt}\". Run it with --non-interactive, and with the arguments for any other answer it needs.");
}
