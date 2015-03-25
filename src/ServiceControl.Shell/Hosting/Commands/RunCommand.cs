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
                    var lockObject = new Object();
                    service.OnStopping = () =>
                    {
                        if (!Monitor.TryEnter(lockObject))
                        {
                            return;
                        }

                        service.OnStopping = () => { };
                        waitHandle.Set();
                        Monitor.Exit(lockObject);
                    };

                    Console.CancelKeyPress += (sender, e) =>
                    {
                        if (!Monitor.TryEnter(lockObject))
                        {
                            return;
                        }

                        service.OnStopping = () => { };
                        e.Cancel = true;
                        waitHandle.Set();
                        Monitor.Exit(lockObject);
                    };

                    service.Run();

                    Console.WriteLine("Press Ctrl+C to exit");
                    waitHandle.WaitOne();
                }
            }
        }
    }
}