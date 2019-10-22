namespace ServiceControlInstaller.Engine
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskHelpers
    {
        public static Task Run(Action action)
        {
            return Task.Factory.StartNew(_ => action(), null, default(CancellationToken), TaskCreationOptions.None, TaskScheduler.Default);
        }
    }
}