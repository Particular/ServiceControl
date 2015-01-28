namespace Particular.ServiceControl.Commands
{
    using System;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    internal class CheckMandatoryInstallOptionsCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        { 
            if (!Settings.ForwardAuditMessages.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Installation Aborted!");
                Console.ResetColor();
                Console.WriteLine(@"
This installation requires addition information:

ForwardAuditMessages must be explicitly set to true or false

e.g.

  ServiceControl.exe -install -d=""ServiceControl/ForwardAuditMessages==true""

For more information go to the documentation site - http://docs.particular.net
and search for 'ServiceControl ForwardAuditMessages'
");
                Environment.Exit(1);
            }
        }
    }
}
