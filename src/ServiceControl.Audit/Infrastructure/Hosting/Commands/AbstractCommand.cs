namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System.Threading.Tasks;

    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args);
    }
}