namespace ServiceControlInstaller.Engine.Database
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using ServiceControlInstaller.Engine.Instances;

    public class DatabaseMigrations
    {
        public static void RunDatabaseMigrations(IServiceControlInstance instance, Action<string> updateProgress)
        {
            var timeout = (int)TimeSpan.FromMinutes(20).TotalMilliseconds;
            var args = $"--database --serviceName={instance.Name}";
            RunDataMigration(updateProgress, instance.InstallPath, Constants.ServiceControlExe, timeout, () => args);
        }

        public static void RunDataMigration(Action<string> updateProgress, string installPath, string exeName, int timeoutMilliseconds, Func<string> args)
        {
            var fileName = Path.Combine(installPath, exeName);
            var attempts = 0;

            do
            {
                using (var p = new Process())
                {
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.FileName = fileName;
                    p.StartInfo.Arguments = args();
                    p.StartInfo.WorkingDirectory = installPath;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;

                    var error = new StringBuilder();

                    using (var outputWaitHandle = new AutoResetEvent(false))
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        p.OutputDataReceived += (sender, eventArgs) =>
                        {
                            var output = eventArgs.Data;
                            if (output == null)
                            {
                                outputWaitHandle.Set();
                            }
                            else
                            {
                                if (!output.Contains("|Error|"))
                                {
                                    updateProgress(SpliceText(output.Replace(":", string.Empty)));
                                }
                            }
                        };

                        p.ErrorDataReceived += (sender, e) =>
                        {
                            if (e.Data == null)
                            {
                                errorWaitHandle.Set();
                            }
                            else
                            {
                                error.AppendLine(e.Data);
                            }
                        };

                        attempts++;

                        Trace.WriteLine($"Attempt {attempts}");

                        p.Start();

                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();

                        if (p.WaitForExit(timeoutMilliseconds) &&
                            outputWaitHandle.WaitOne(timeoutMilliseconds) &&
                            errorWaitHandle.WaitOne(timeoutMilliseconds))
                        {
                            Trace.WriteLine($"Attempt {attempts} exited with code {p.ExitCode}");
                            if (p.ExitCode == 0)
                            {
                                break;
                            }

                            if (attempts < 2)
                            {
                                continue;
                            }

                            if (p.ExitCode != 0)
                            {
                                throw new DatabaseMigrationsException($"{exeName} threw an error when migrating data. Please contact Particular support. The error output from {exeName} was:\r\n {error}");
                            }
                        }
                        else
                        {
                            throw new DatabaseMigrationsTimeoutException($"{exeName} timed out while migrating data.");
                        }
                    }
                }
            } while (attempts < 2);
        }

        private static string SpliceText(string text)
        {
            return SpliceTextPattern.Replace(text, $"$1{Environment.NewLine}");
        }

        // line length = 80
        static readonly Regex SpliceTextPattern = new Regex($"(.{{80}})", RegexOptions.Compiled);
    }
}