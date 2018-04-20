namespace ServiceControlInstaller.Engine.Database
{
	using System;
	using System.Diagnostics;
	using System.IO;
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
			var processStartupInfo = new ProcessStartInfo
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				FileName = Path.Combine(installPath, exeName),
				Arguments = $"--database --serviceName={serviceName}",
				WorkingDirectory = installPath,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			var attempts = 0;
			Process p;
			var updatingSchema = false;
			do
			{
				attempts++;

				p = Process.Start(processStartupInfo);
				if (p == null)
				{
					throw new Exception($"Attempt to launch {exeName} failed.");
				}

				p.OutputDataReceived += (sender, eventArgs) =>
				{
					if (eventArgs.Data.StartsWith("Updating schema from version"))
					{
						updatingSchema = true;
						updateProgress(eventArgs.Data.Remove(':'));
					}
					else if (updatingSchema && eventArgs.Data.StartsWith("OK"))
					{
						updatingSchema = false;
						updateProgress(string.Empty);
					}
				};

				p.WaitForExit((int)TimeSpan.FromMinutes(20).TotalMilliseconds);
			} while (p.ExitCode > 0 && attempts < 2);

			if (p.ExitCode != 0)
			{
				var error = p.StandardError.ReadToEnd();
				throw new DatabaseMigrationsException($"{exeName} threw an error when migrating data. Please contact Particular support. The error output from {exeName} was:\r\n {error}");
			}
		}
	}
}
