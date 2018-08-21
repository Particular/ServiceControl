namespace ServiceControl.Config.Framework.Commands
{
    using System;
    using System.Threading.Tasks;

    abstract class AwaitableAbstractCommand<T> : BaseCommand<T>, ICommand<T>
    {
        protected AwaitableAbstractCommand(Func<T, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        bool System.Windows.Input.ICommand.CanExecute(object parameter)
        {
            return CanExecute((T)parameter);
        }

        async void ICommand<T>.Execute(T obj)
        {
            using (StartExecuting())
            {
                await ExecuteAsync(obj);
            }
        }

        void System.Windows.Input.ICommand.Execute(object parameter)
        {
            ((ICommand<T>)this).Execute((T)parameter);
        }

        public abstract Task ExecuteAsync(T obj);
    }
}