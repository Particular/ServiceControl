namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.IO;
    using System.Reflection;

    static class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            try
            {
                var arguments = new HostArguments(args);

                var settings = LoadSettings(ConfigurationManager.AppSettings, arguments);

                var runAsWindowsService = !Environment.UserInteractive && !arguments.Portable;
                MonitorLogs.Configure(settings, !runAsWindowsService);

                var runner = new CommandRunner(arguments.Commands);

                runner.Run(settings).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Environment.Exit(-1);
            }
        }

        static Settings LoadSettings(NameValueCollection config, HostArguments args)
        {
            var reader = new SettingsReader(config);
            var settings = Settings.Load(reader);
            args.ApplyOverridesTo(settings);
            return settings;
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            var combine = Path.Combine(appDirectory, requestingName + ".dll");
            return !File.Exists(combine) ? null : Assembly.LoadFrom(combine);
        }
    }
}