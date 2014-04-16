namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.ServiceProcess;
    using Particular.ServiceControl.Commands;
    using Particular.ServiceControl.Hosting;

    class StartCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            if (!ServiceUtils.IsServiceInstalled(args.ServiceName))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Out.WriteLine("The '{0}' service is not installed.", args.ServiceName);
                Console.ResetColor();

                return;
            }

            var stopController = new ServiceController(args.ServiceName);

            if (stopController.Status != ServiceControllerStatus.Running)
            {
                stopController.Start();
                stopController.WaitForStatus(ServiceControllerStatus.Running);
            }

            Console.Out.WriteLine("Service started");    
        }
    }
}