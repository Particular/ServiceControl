namespace Particular.ServiceControl.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.ServiceProcess;
    using System.Text;
    using Commands;

    internal class HostArguments
    {
        public HostArguments(string[] args)
        {
            var executionMode = ExecutionMode.Run;

            commands = new List<Type> { typeof(RunCommand) };
            startMode = StartMode.Automatic;
            ServiceName = "Particular.ServiceControl";
            DisplayName = "Particular ServiceControl";
            Description = "Particular Software ServiceControl for NServiceBus.";
            ServiceAccount = ServiceAccount.LocalSystem;
            Username = String.Empty;
            Password = String.Empty;

            var runOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
                },
            };

            uninstallOptions = new OptionSet
            {
                {
                    "?|h|help", "Help about the command line options.", key => { Help = true; }
                },
                {
                    "u|uninstall",
                    @"Uninstall the endpoint as a Windows service."
                    , s =>
                    {
                        commands = new List<Type> {typeof(UninstallCommand)};
                        executionMode = ExecutionMode.Uninstall;
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service."
                    , s => { ServiceName = s; }
                }
            };

            installOptions = new OptionSet
            {
                {
                    "?|h|help",
                    "Help about the command line options.",
                    key => { Help = true; }
                },
                {
                    "i|install",
                    @"Install the endpoint as a Windows service."
                    , s =>
                    {
                        commands = new List<Type> { typeof(RunBootstrapper), typeof(InstallCommand) };
                        executionMode = ExecutionMode.Install;
                    }
                },
                {
                    "serviceName=",
                    @"Specify the service name for the installed service."
                    , s => { ServiceName = s; }
                },
                {
                    "displayName=",
                    @"Friendly name for the installed service."
                    , s => { DisplayName = s; }
                },
                {
                    "description=",
                    @"Description for the service."
                    , s => { Description = s; }
                },
                {
                    "username=",
                    @"Username for the account the service should run under."
                    , s => { Username = s; }
                },
                {
                    "password=",
                    @"Password for the service account."
                    , s => { Password = s; }
                },
                {
                    "localservice",
                    @"Run the service with the local service account."
                    , s => { ServiceAccount = ServiceAccount.LocalService; }
                },
                {
                    "networkservice",
                    @"Run the service with the network service permission."
                    , s => { ServiceAccount = ServiceAccount.NetworkService; }
                },
                {
                    "user",
                    @"Run the service with the specified username and password. Alternative the system will prompt for a valid username and password if values for both the username and password are not specified."
                    , s => { ServiceAccount = ServiceAccount.User; }
                },
                {
                    "delayed",
                    @"The service should start automatically (delayed)."
                    , s => { startMode = StartMode.Delay; }
                },
                {
                    "autostart",
                    @"The service should start automatically (default)."
                    , s => { startMode = StartMode.Automatic; }
                },
                {
                    "disabled",
                    @"The service should be set to disabled."
                    , s => { startMode = StartMode.Disabled; }
                },
                {
                    "manual",
                    @"The service should be started manually."
                    , s => { startMode = StartMode.Manual; }
                },
            };

            try
            {
                installOptions.Parse(args);
                if (executionMode == ExecutionMode.Install)
                {
                    return;
                }

                uninstallOptions.Parse(args);
                if (executionMode == ExecutionMode.Uninstall)
                {
                    return;
                }

                runOptions.Parse(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Help = true;
            }
        }

        public List<Type> Commands
        {
            get { return commands; }
        }

        public bool Help { get; set; }
        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        public StartMode StartMode
        {
            get { return startMode; }
        }

        public string OutputPath { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public ServiceAccount ServiceAccount { get; set; }

        public void PrintUsage()
        {
            var sb = new StringBuilder();

            var helpText = String.Empty;
            using (
                var stream =
                    Assembly.GetCallingAssembly()
                        .GetManifestResourceStream("ServiceControl.Hosting.Help.txt"))
            {
                if (stream != null)
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        helpText = streamReader.ReadToEnd();
                    }
                }
            }

            installOptions.WriteOptionDescriptions(new StringWriter(sb));
            var installOptionsHelp = sb.ToString();

            sb.Clear();
            uninstallOptions.WriteOptionDescriptions(new StringWriter(sb));
            var uninstallOptionsHelp = sb.ToString();

            Console.Out.WriteLine(helpText, installOptionsHelp, uninstallOptionsHelp);
        }

        readonly OptionSet installOptions;
        readonly OptionSet uninstallOptions;
        List<Type> commands;
        StartMode startMode;
    }

    internal enum ExecutionMode
    {
        Install,
        Uninstall,
        Extract,
        Run
    }

    internal enum StartMode
    {
        Manual,
        Automatic,
        Delay,
        Disabled
    }
}