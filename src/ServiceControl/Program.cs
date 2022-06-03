namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Commands;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    public class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            var loggingSettings = new LoggingSettings(arguments.ServiceName, logToConsole: !arguments.RunAsWindowsService);
            LoggingConfigurator.ConfigureLogging(loggingSettings);

            await new CommandRunner(arguments.Commands).Execute(arguments)
                .ConfigureAwait(false);
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            var combine = Path.Combine(appDirectory, requestingName + ".dll");
            if (!File.Exists(combine))
            {
                return null;
            }

            return Assembly.LoadFrom(combine);
        }
    }
}