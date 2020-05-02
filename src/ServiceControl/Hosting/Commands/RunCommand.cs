namespace Particular.ServiceControl.Commands
{
    using System;
    using System.Threading.Tasks;
    using Hosting;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            if (!args.Portable && !Environment.UserInteractive) // Windows Service
            {
                RunNonBlocking(args);
            }

            // Interactive or non-interactive portable (e.g. docker)
            await RunAndWait(args).ConfigureAwait(false);
        }

        static void RunNonBlocking(HostArguments args)
        {
            using (var service = new Host(false) { ServiceName = args.ServiceName })
            {
                service.Run();
            }
        }

        static async Task RunAndWait(HostArguments args)
        {
            using (var service = new Host(true) { ServiceName = args.ServiceName })
            {
                var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                service.OnStopping = () =>
                {
                    service.OnStopping = () => { };
                    completionSource.TrySetResult(true);
                };

                service.Run();

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