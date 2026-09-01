namespace ServiceControl.AcceptanceTests.RavenDB
{
    using System;
    using System.Diagnostics;
    using NUnit.Framework;

    [TestFixture]
    class DiagPath
    {
        static void Run(string label, string file, string args)
        {
            Console.WriteLine($"=== {label}: {file} {args} ===");
            var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            try
            {
                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("NETCore.App") || line.Contains("runtimes installed") || line.Contains("Base Path"))
                    {
                        Console.WriteLine("  " + line.Trim());
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("  FAILED: " + e.Message);
            }
        }

        [Test]
        public void PrintPathAndDotnetInfo()
        {
            Console.WriteLine("DIAG PATH=" + Environment.GetEnvironmentVariable("PATH"));
            Console.WriteLine("DIAG CWD=" + Environment.CurrentDirectory);
            Run("PATH-dotnet", "dotnet", "--info");
            Run("tmp-wrap", "/tmp/wrap/dotnet", "--info");
            Run("tmp-dotnet8", "/tmp/dotnet8/dotnet", "--info");
            Run("home-dotnet", "/home/piuser/.dotnet/dotnet", "--info");
            Assert.Pass();
        }
    }
}