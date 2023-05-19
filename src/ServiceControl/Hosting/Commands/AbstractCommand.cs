namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args, Settings settings);
    }
}