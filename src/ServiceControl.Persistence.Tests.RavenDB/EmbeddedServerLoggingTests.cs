namespace ServiceControl.Persistence.Tests.RavenDB;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
class EmbeddedServerLoggingTests
{
    [Test]
    public async Task Server_writes_log_files_to_the_configured_log_path()
    {
        // The shared server starts with SharedEmbeddedServer.LogsMode and SharedEmbeddedServer.LogPath
        await SharedEmbeddedServer.GetInstance();

        // Log files from earlier test runs stay in the folder, so only files written by this run count
        var startedAfter = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();

        while (!HasRecentLogFile(startedAfter) && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.That(HasRecentLogFile(startedAfter), Is.True,
            $"The server should write a log file to {SharedEmbeddedServer.LogPath} at the {SharedEmbeddedServer.LogsMode} level within {timeout.TotalSeconds} seconds");
    }

    static bool HasRecentLogFile(DateTime startedAfter) =>
        Directory.Exists(SharedEmbeddedServer.LogPath)
        && new DirectoryInfo(SharedEmbeddedServer.LogPath)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Any(file => file.Length > 0 && file.LastWriteTimeUtc >= startedAfter);
}
