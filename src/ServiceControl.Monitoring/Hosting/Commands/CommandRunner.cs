namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class CommandRunner(Type commandType)
    {
        public async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            var command = (AbstractCommand)Activator.CreateInstance(commandType);
            await command.Execute(args, settings, cancellationToken);
        }
    }
}