using System.Threading.Tasks;

namespace ServiceControl.Hosting.Commands
{
    abstract class AbstractCommand
    {
        public abstract Task Execute(HostArguments args);
    }
}