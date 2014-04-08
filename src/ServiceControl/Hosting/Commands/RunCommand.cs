namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Threading;
    using Hosting;

    internal class RunCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            if (!Environment.UserInteractive)
            {
                using (var service = new Host())
                {
                    service.Run();
                }

                return;
            }

            using (var service = new Host())
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    service.OnStopping = () =>
                    {
                        service.OnStopping = () => { };
                        waitHandle.Set();
                    };

                    service.Run();

                    Console.CancelKeyPress += (sender, e) =>
                    {
                        service.OnStopping = () => { };
                        e.Cancel = true;
                        waitHandle.Set();
                    };

                    Console.WriteLine("Press Ctrl+C to exit");
                    waitHandle.WaitOne();
                }
            }
        }
    }
}