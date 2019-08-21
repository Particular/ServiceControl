namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class CommandRunner
    {
        List<Type> commands;

        public CommandRunner(List<Type> commands)
        {
            this.commands = commands;
        }

        public async Task Run(Settings settings)
        {
            foreach (var commandType in commands)
            {
                var command = (AbstractCommand)Activator.CreateInstance(commandType);
                await command.Execute(settings);
            }
        }
    }
}