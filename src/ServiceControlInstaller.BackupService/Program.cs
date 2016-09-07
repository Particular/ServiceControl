namespace ServiceControlInstaller.BackupStubService
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.ServiceProcess;

    class Program
    {
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            using (var service = new StubService())
            {
                if (Environment.UserInteractive)
                {
                    service.InteractiveStart();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                    service.InteractiveStop();
                    return;
                }
                ServiceBase.Run(service);
            }
        }

        static Assembly ResolveAssembly(string name)
        {
                var assemblyLocation = Assembly.GetEntryAssembly().Location;
                var appDirectory = Path.GetDirectoryName(assemblyLocation);
                var requestingName = new AssemblyName(name).Name;
                // ReSharper disable once AssignNullToNotNullAttribute
                var combine = Path.Combine(appDirectory, requestingName + ".dll");
                return !File.Exists(combine) ? null : Assembly.LoadFrom(combine);
        }
    }
}
