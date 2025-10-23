namespace ServiceControl.Hosting.Commands
{
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args);
    }
}