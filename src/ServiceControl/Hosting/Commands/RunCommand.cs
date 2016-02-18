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
                using (var service = new Host{ServiceName = args.ServiceName})
                {
                    service.Run();
                }

                return;
            }

            using (var service = new Host{ ServiceName = args.ServiceName} )
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    service.OnStopping = () =>
                    {
                        service.OnStopping = () => { };
                        waitHandle.Set();
                    };

                    service.Run();

                    var r = new CancelWrapper(waitHandle, service);
                    Console.CancelKeyPress += r.ConsoleOnCancelKeyPress;

                    Console.WriteLine("Press Ctrl+C to exit");
                    waitHandle.WaitOne();
                }
            }
        }

        class CancelWrapper
        {
            private readonly ManualResetEvent manualReset;
            private readonly Host host;

            public CancelWrapper(ManualResetEvent manualReset, Host host)
            {
                this.manualReset = manualReset;
                this.host = host;
            }

            public void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
            {
                host.OnStopping = () => { };
                e.Cancel = true;
                manualReset.Set();
                Console.CancelKeyPress -= ConsoleOnCancelKeyPress;
            } 
        }
    }
}