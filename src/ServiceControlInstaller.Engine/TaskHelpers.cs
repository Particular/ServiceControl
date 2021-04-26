namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskHelpers
    {
        public static Task Run(Action action, CancellationToken cancellationToken = default)
        {
            return Task.Factory.StartNew(_ => action(), null, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
        }
    }
}