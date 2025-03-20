namespace ServiceControl.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public static class IntegratedSetup
    {
        const string SetupAndRunCmd = "--setup-and-run";
        const string SetupCmd = "--setup";

        public static async Task<int> Run()
        {
            // Using GetCommandLineArgs instead of the args passed into Main because GetCommandLineArgs provides the entry assembly path
            var args = Environment.GetCommandLineArgs().ToList();

            if (!args.Contains(SetupAndRunCmd))
            {
                return 0;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (args[i] == SetupAndRunCmd)
                {
                    args[i] = SetupCmd;
                }
            }

            var processPath = Environment.ProcessPath;

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

            await process.WaitForExitAsync();

            return process.ExitCode;
        }
    }
}