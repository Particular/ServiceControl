﻿namespace ServiceControl.Audit.Infrastructure.Hosting.Commands
{
    using System;
    using System.ServiceProcess;
    using System.Threading.Tasks;

    class RunCommand<T> : AbstractCommand
        where T : ServiceBase, IStartableStoppableService, new()
    {
        public override async Task Execute(HostArguments args)
        {
            if (args.RunAsWindowsService)
            {
                using (var service = new T { ServiceName = args.ServiceName })
                {
                    //HINT: this calls-back to Windows Service Control Manager (SCM) and hangs
                    //      until service reports it has stopped.
                    //      SCM takes over and calls OnStart and OnStop on the instance. 
                    ServiceBase.Run(service);
                }
            }
            else
            {
                await RunAsConsoleApp(args).ConfigureAwait(false);
            }
        }

        static async Task RunAsConsoleApp(HostArguments args)
        {
            using (var service = new T { ServiceName = args.ServiceName })
            {
                var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                service.OnStopping = () =>
                {
                    service.OnStopping = () => { };
                    completionSource.TrySetResult(true);
                };

                await service.Start().ConfigureAwait(false);

                var r = new CancelWrapper(completionSource, service);
                Console.CancelKeyPress += r.ConsoleOnCancelKeyPress;

                Console.WriteLine("Press Ctrl+C to exit");
                await completionSource.Task.ConfigureAwait(false);
            }
        }

        class CancelWrapper
        {
            public CancelWrapper(TaskCompletionSource<bool> syncEvent, T host)
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
            readonly T host;
        }
    }
}