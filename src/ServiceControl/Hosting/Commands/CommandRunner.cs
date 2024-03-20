namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class CommandRunner(List<Type> commands)
    {
        public async Task Execute(HostArguments args, Settings settings, LoggingSettings loggingSettings)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(args, settings, loggingSettings);
            }
        }
    }
}