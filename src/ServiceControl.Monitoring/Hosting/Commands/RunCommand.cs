namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            if (Environment.UserInteractive) // Interactive or non-interactive portable (e.g. docker)
            {
                return RunAndWait(settings);
            }

            // Windows Service
            return RunNonBlocking(settings);
        }

        Task RunNonBlocking(Settings settings)
        {
            using (var service = new Host(false)
            {
                Settings = settings,
                ServiceName = settings.ServiceName
            })
            {
                service.Run();
            }

            return Task.FromResult(0);
        }

        async Task RunAndWait(Settings settings)
        {
            using (var service = new Host(true)
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

                service.Run();

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