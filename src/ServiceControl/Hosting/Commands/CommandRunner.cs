namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class CommandRunner
    {
        public CommandRunner(List<Type> commands)
        {
            this.commands = commands;
        }

        public async Task Execute(HostArguments args, Settings settings)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(args, settings).ConfigureAwait(false);
            }
        }

        readonly List<Type> commands;
    }
}