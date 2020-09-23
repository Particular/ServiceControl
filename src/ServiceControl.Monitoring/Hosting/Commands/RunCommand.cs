namespace ServiceControl.Monitoring
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            if (settings.RunAsWindowsService)
            {
                using (var service = new Host { Settings = settings, ServiceName = settings.ServiceName})
                {
                    //HINT: this calls-back to Windows Service Control Manager (SCM) and hangs
                    //      until service reports it has stopped.
                    //      SCM takes over and calls OnStart and OnStop on the service instance. 
                    ServiceBase.Run(service);
                }
            }
            else
            {
                return RunAsConsoleApp(settings);
            }

            return Task.CompletedTask;
        }

        async Task RunAsConsoleApp(Settings settings)
        {
            using (var service = new Host
            {
                Settings = settings,
                ServiceName = settings.ServiceName
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

                service.Start();

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