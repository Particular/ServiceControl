namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;

    class CommandRunner(Type commandType)
    {
        public async Task Execute(HostArguments args)
        {
            var command = (AbstractCommand)Activator.CreateInstance(commandType);
            await command.Execute(args);
        }
    }
}