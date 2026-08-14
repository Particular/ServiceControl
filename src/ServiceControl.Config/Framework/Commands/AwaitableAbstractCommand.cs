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

#pragma warning disable PS0027 // ICommand.Execute returns void, so there is nothing to return the task to
        async void ICommand<T>.Execute(T obj)
        {
            using (StartExecuting())
            {
                OnExecuting();
                await ExecuteAsync(obj);

            }
        }
#pragma warning restore PS0027

        void System.Windows.Input.ICommand.Execute(object parameter)
        {
            ((ICommand<T>)this).Execute((T)parameter);
        }

#pragma warning disable PS0018
        public abstract Task ExecuteAsync(T obj);
#pragma warning restore PS0018

        protected virtual void OnExecuting()
        {
            OnCommandExecuting();
        }

        public Action OnCommandExecuting = () => { };
    }
}