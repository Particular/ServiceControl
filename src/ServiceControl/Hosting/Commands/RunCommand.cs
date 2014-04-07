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
                using (waitHandle)
                {
                    service.OnStopping = () =>
                    {
                        service.OnStopping = () => { };
                        waitHandle.Set();
                    };

                    service.Run();

                    Console.CancelKeyPress += ConsoleOnCancelKeyPress;

                    Console.WriteLine("Press Ctrl+C to exit");
                    waitHandle.WaitOne();
                }
            }
        }

        void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.CancelKeyPress -= ConsoleOnCancelKeyPress;

            e.Cancel = true;
            waitHandle.Set();
        }

        ManualResetEvent waitHandle = new ManualResetEvent(false);
    }
}