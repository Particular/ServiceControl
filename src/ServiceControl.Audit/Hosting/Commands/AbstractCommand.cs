namespace Particular.ServiceControl.Commands
{
    using System.Threading.Tasks;
    using Hosting;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args);
    }
}