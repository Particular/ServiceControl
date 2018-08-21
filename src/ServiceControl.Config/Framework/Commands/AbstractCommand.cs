namespace ServiceControl.Config.Framework.Commands
{
    using System;

    abstract class AbstractCommand<T> : BaseCommand<T>, ICommand<T>
    {
        public AbstractCommand(Func<T, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        bool System.Windows.Input.ICommand.CanExecute(object parameter)
        {
            return CanExecute((T)parameter);
        }

        void ICommand<T>.Execute(T obj)
        {
            using (StartExecuting())
            {
                OnExecuting();
                Execute(obj);
            }
        }

        void System.Windows.Input.ICommand.Execute(object parameter)
        {
            ((ICommand<T>)this).Execute((T)parameter);
        }

        public abstract void Execute(T obj);

        protected virtual void OnExecuting()
        {
            OnCommandExecuting();
        }

        public Action OnCommandExecuting = () => { };
    }
}