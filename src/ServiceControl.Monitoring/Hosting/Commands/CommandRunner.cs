namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class CommandRunner
    {
        public CommandRunner(List<Type> commands)
        {
            this.commands = commands;
        }

        public async Task Execute(Settings settings)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(settings);
            }
        }

        List<Type> commands;
    }
}