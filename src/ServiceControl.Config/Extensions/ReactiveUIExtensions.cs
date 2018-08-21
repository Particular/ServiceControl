namespace ServiceControl.Config
{
    using System;
    using System.Threading.Tasks;
    using ReactiveUI;

    static class ReactiveUIExtensions
    {
        public static ReactiveCommand DoAction(this ReactiveCommand command, Action<object> action)
        {
            command.Subscribe(action);
            return command;
        }

        public static ReactiveCommand DoAsync(this ReactiveCommand command, Func<object, Task> action)
        {
            command.RegisterAsyncTask(action);
            return command;
        }
    }
}