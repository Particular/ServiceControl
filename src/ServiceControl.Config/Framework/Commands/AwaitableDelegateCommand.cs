namespace ServiceControl.Config.Framework.Commands
{
    using System;
    using System.Threading.Tasks;

    class AwaitableDelegateCommand : AwaitableDelegateCommand<object>, IAsyncCommand
    {
        public AwaitableDelegateCommand(Func<object, Task> executeMethod, Func<object, bool> canExecuteMethod = null) : base(executeMethod, canExecuteMethod)
        {
        }
    }

    class AwaitableDelegateCommand<T> : BaseCommand<T>, IAsyncCommand<T>
    {
        public AwaitableDelegateCommand(Func<T, Task> executeMethod, Func<T, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
            this.executeMethod = executeMethod ?? throw new ArgumentNullException(nameof(executeMethod), @"Execute Method cannot be null");
        }

        bool System.Windows.Input.ICommand.CanExecute(object parameter)
        {
            return CanExecute((T)parameter);
        }

        async void System.Windows.Input.ICommand.Execute(object parameter)
        {
            await ExecuteAsync((T)parameter);
        }

        public async Task ExecuteAsync(T parameter)
        {
            using (StartExecuting())
            {
                await executeMethod(parameter);
            }
        }

        readonly Func<T, Task> executeMethod;
    }
}