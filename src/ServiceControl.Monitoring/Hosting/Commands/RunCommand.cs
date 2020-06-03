namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            var consoleSession = settings.Portable || Environment.UserInteractive;
            if (consoleSession)
            {
                // Regular console (interactive) & Docker (non-interactive)
                return RunAsConsole(settings);
            }
            else
            {
                // Windows Service
                return RunAsService(settings);
            }
        }

        Task RunAsService(Settings settings)
        {
            using (var service = new Host()
            {
                Settings = settings,
                ServiceName = settings.ServiceName
            })
            {
                service.RunAsService();
            }

            return Task.FromResult(0);
        }

        async Task RunAsConsole(Settings settings)
        {
            using (var service = new Host()
            {
                Settings = settings,
                ServiceName = settings.ServiceName,
            })
            {
                var tcs = new TaskCompletionSource<bool>();

                Action done = () =>
                {
                    service.OnStopping = () => { };
                    tcs.SetResult(true);
                };

                service.OnStopping = done;

                OnConsoleCancel.Run(done);

                service.RunAsConsole();

                Console.WriteLine("Press Ctrl+C to exit");

                await tcs.Task.ConfigureAwait(false);
            }
        }

        class OnConsoleCancel
        {
            OnConsoleCancel(Action action)
            {
                this.action = action;
            }

            public static void Run(Action action)
            {
                var onCancelAction = new OnConsoleCancel(action);
                Console.CancelKeyPress += onCancelAction.ConsoleOnCancelKeyPress;
            }

            void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
            {
                action();
                e.Cancel = true;
                Console.CancelKeyPress -= ConsoleOnCancelKeyPress;
            }

            Action action;
        }
    }
}