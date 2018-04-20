namespace ServiceControlInstaller.Engine.Database
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using ServiceControlInstaller.Engine.Instances;

    internal class DatabaseMigrations
    {
        public static void RunDatabaseMigrations(IServiceControlInstance instance, Action<string> updateProgress)
        {
            RunDataMigration(updateProgress, instance.InstallPath,
                Constants.ServiceControlExe,
                instance.Name);
        }

        static void RunDataMigration(Action<string> updateProgress, string installPath, string exeName, string serviceName)
        {
            var fileName = Path.Combine(installPath, exeName);
            var args = $"--database --serviceName={serviceName}";

            var attempts = 0;
            var timeout = (int) TimeSpan.FromMinutes(20).TotalMilliseconds;

            do
            {
                using (var p = new Process())
                {
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.FileName = fileName;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.WorkingDirectory = installPath;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;

                    var error = new StringBuilder();

                    var updatingSchema = false;

                    using (var outputWaitHandle = new AutoResetEvent(false))
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        p.OutputDataReceived += (sender, eventArgs) =>
                        {
                            if (eventArgs.Data == null)
                            {
                                outputWaitHandle.Set();
                            }
                            else
                            {

                                Debug.WriteLine(eventArgs.Data);

                                if (eventArgs.Data.StartsWith("Updating schema from version"))
                                {
                                    updatingSchema = true;
                                    updateProgress(eventArgs.Data.Replace(":", string.Empty));
                                }
                                else if (updatingSchema && eventArgs.Data.StartsWith("OK"))
                                {
                                    updatingSchema = false;
                                    updateProgress(string.Empty);
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

                        Debug.WriteLine($"Attempt {attempts}");

                        p.Start();

                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();

                        if (p.WaitForExit(timeout) &&
                            outputWaitHandle.WaitOne(timeout) &&
                            errorWaitHandle.WaitOne(timeout))
                        {
                            Debug.WriteLine($"Attempt {attempts} exited with code {p.ExitCode}");
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
    }
}
