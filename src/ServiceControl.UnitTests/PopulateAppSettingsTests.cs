namespace ServiceControl.UnitTests;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class PopulateAppSettingsTests
{
    [Test]
    public async Task Should_populate_appSettings_from_exe_config_file()
    {
        const string MagicValue = "7303A0AA-1003-4DC4-823B-4E8B2A35CF57";

        var config = $"""
                      <?xml version="1.0" encoding="utf-8"?>
                      <configuration>
                        <appSettings>
                          <add key="ServiceControl/LogPath" value="{MagicValue}"   />
                        </appSettings>
                      </configuration>
                      """;

        await File.WriteAllTextAsync("ServiceControl.exe.config", config);

        var fileName = "ServiceControl";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName = "ServiceControl.exe";
        }

        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        var p = Process.Start(startInfo);

        if (p == null)
        {
            throw new Exception("Failed to start ServiceControl");
        }

        var pathIsSet = false;

        var outputTask = Task.Run(async () =>
        {
            while (!p.StandardOutput.EndOfStream)
            {
                var line = await p.StandardOutput.ReadLineAsync();

                if (line.Contains($"Logging to {MagicValue}"))
                {
                    pathIsSet = true;
                    p.Kill(true);
                }
            }
        });

        if (!p.WaitForExit(5000))
        {
            p.Kill(true);
        }

        await outputTask;

        Assert.That(pathIsSet, Is.True);
    }

    [TearDown]
    public void TearDown() => File.Delete("ServiceControl.exe.config");
}