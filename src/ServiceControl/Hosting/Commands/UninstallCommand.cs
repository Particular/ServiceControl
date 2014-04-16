namespace Particular.ServiceControl.Commands
{
    using System;
    using Hosting;

    internal class UninstallCommand : ServiceCommand
    {
        public UninstallCommand() : base(installer => installer.Uninstall(null))
        {
        }

        public override void Execute(HostArguments args)
        {
            if (!ServiceUtils.IsServiceInstalled(args.ServiceName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteLine("The '{0}' service is not installed.", args.ServiceName);
                Console.ResetColor();

                return;
            }

            ExecuteInternal(args);
        }
    }
}