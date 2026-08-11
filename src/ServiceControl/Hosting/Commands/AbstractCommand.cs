namespace ServiceControl.Hosting.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default);
    }
}