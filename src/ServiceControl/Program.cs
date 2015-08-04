namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using Commands;
    using Hosting;

    public class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            new CommandRunner(arguments.Commands).Execute(arguments);
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            // ReSharper disable once AssignNullToNotNullAttribute
            var combine = Path.Combine(appDirectory, requestingName + ".dll");
            if (!File.Exists(combine))
            {
                return null;
            }
            return Assembly.LoadFrom(combine);
        }
    }
}