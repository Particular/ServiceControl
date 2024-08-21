namespace ServiceControl.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public static class IntegratedSetup
    {
        const string ArgName = "--integrated-setup";

        public static Task Run()
        {
            // Contains DLL if executed as `dotnet PATH`, which args passed to Main() do not
            var args = Environment.GetCommandLineArgs().ToList();
            if (!args.Contains(ArgName))
            {
                return Task.CompletedTask;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] == ArgName)
                {
                    args[i] = "--setup";
                }
            }

            var processPath = Environment.ProcessPath;
            var commandLine = Environment.CommandLine.Replace(ArgName, "--setup");
            if (!Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                args.RemoveAt(0);
            }

            var startInfo = new ProcessStartInfo(processPath, args)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);

            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.Error.WriteLine(e.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process.WaitForExitAsync();
        }
    }
}