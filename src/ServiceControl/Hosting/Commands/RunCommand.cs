namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Threading;
    using Hosting;

    internal class RunCommand : AbstractCommand
    {
        public override void Execute(HostArguments args)
        {
            using (var service = new Host())
            {
                service.Run();

                if (!Environment.UserInteractive)
                {
                    return;
                }

                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    waitHandle.Set();
                };

                Console.WriteLine("Press Ctrl+C to exit");
                waitHandle.WaitOne();
                waitHandle.Dispose();
            }
        }

        ManualResetEvent waitHandle = new ManualResetEvent(false);
    }
}