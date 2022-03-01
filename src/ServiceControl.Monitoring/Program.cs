namespace ServiceControl.Monitoring
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;

    static class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            var arguments = new HostArguments(args);

            var settings = LoadSettings(arguments);

            var runAsWindowsService = !Environment.UserInteractive && !arguments.Portable;
            MonitorLogs.Configure(settings, !runAsWindowsService);

            await new CommandRunner(arguments.Commands)
                .Run(settings)
                .ConfigureAwait(false);
        }

        static Settings LoadSettings(HostArguments args)
        {
            var settings = new Settings();
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