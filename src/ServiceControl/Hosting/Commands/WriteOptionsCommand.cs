namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Configuration;
    using Hosting;

    class WriteOptionsCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            if (args.Options.Count == 0)
            {
                return;
            }
            var configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            foreach (var option in args.Options)
            {
                if (option.Key == "NServiceBus/Transport")
                {
                    configuration.ConnectionStrings.ConnectionStrings.Remove("NServiceBus/Transport");
                    configuration.ConnectionStrings.ConnectionStrings.Add(new ConnectionStringSettings("NServiceBus/Transport", option.Value));
                    return;
                }

                configuration.AppSettings.Settings.Remove(option.Key);
                configuration.AppSettings.Settings.Add(new KeyValueConfigurationElement(option.Key, option.Value));
            }

            configuration.Save();

            Console.Out.WriteLine("Options written to config file.");
        }
    }
}