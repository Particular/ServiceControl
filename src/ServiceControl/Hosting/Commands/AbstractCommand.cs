namespace Particular.ServiceControl.Commands
{
    using Hosting;

    abstract class AbstractCommand
    {
        public abstract void Execute(HostArguments args);
    }
}