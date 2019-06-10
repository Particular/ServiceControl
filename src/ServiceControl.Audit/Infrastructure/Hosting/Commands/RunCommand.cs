namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.Threading.Tasks;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            if (!args.Portable && !Environment.UserInteractive)
            {
                RunNonBlocking(args);
            }

            await RunAndWait(args).ConfigureAwait(false);
        }

        static void RunNonBlocking(HostArguments args)
        {
            using (var service = new Host {ServiceName = args.ServiceName})
            {
                service.Run(false);
            }
        }

        static async Task RunAndWait(HostArguments args)
        {
            using (var service = new Host {ServiceName = args.ServiceName})
            {
                var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                service.OnStopping = () =>
                {
                    service.OnStopping = () => { };
                    completionSource.TrySetResult(true);
                };

                service.Run(args.Portable || Environment.UserInteractive);

                var r = new CancelWrapper(completionSource, service);
                Console.CancelKeyPress += r.ConsoleOnCancelKeyPress;

                Console.WriteLine("Press Ctrl+C to exit");
                await completionSource.Task.ConfigureAwait(false);
            }
        }

        class CancelWrapper
        {
            public CancelWrapper(TaskCompletionSource<bool> syncEvent, Host host)
            {
                this.syncEvent = syncEvent;
                this.host = host;
            }

            public void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
            {
                host.OnStopping = () => { };
                e.Cancel = true;
                syncEvent.TrySetResult(true);
                Console.CancelKeyPress -= ConsoleOnCancelKeyPress;
            }

            readonly TaskCompletionSource<bool> syncEvent;
            readonly Host host;
        }
    }
}