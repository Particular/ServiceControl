namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using NLog;
    using System;
    using System.Threading.Tasks;

    class RunCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args)
        {
            var consoleSession = args.Portable || Environment.UserInteractive;
            if (consoleSession)
            {
                // Regular console (interactive) & Docker (non-interactive)
                await RunAsConsole(args).ConfigureAwait(false);
            }
            else
            {
                // Windows Service
                RunAsService(args);
            }
        }

        static void RunAsService(HostArguments args)
        {
            using (var service = new Host() { ServiceName = args.ServiceName })
            {
                service.RunAsService();
            }
        }

        static async Task RunAsConsole(HostArguments args)
        {
            using (var service = new Host() { ServiceName = args.ServiceName })
            {
                var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                service.OnStopping = () =>
                {
                    service.OnStopping = () => { };
                    completionSource.TrySetResult(true);
                };

                service.RunAsConsole();

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