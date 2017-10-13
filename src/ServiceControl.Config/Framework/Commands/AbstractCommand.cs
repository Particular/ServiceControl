using System;

namespace ServiceControl.Config.Framework.Commands
{
    internal abstract class AbstractCommand<T> : BaseCommand<T>, ICommand<T>
    {
        public Action OnCommandExecuting = () => { };

        public AbstractCommand(Func<T, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
        }

        public abstract void Execute(T obj);

        protected virtual void OnExecuting()
        {
            OnCommandExecuting();
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
    }
}