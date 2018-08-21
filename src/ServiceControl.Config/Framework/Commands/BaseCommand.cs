namespace ServiceControl.Config.Framework.Commands
{
    using System;
    using System.Reactive.Disposables;
    using System.Windows.Input;

    abstract class BaseCommand<T> : IRaiseCanExecuteChanged
    {
        public BaseCommand(Func<T, bool> canExecuteMethod = null)
        {
            this.canExecuteMethod = canExecuteMethod;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(T parameter)
        {
            if (isExecuting)
            {
                return false;
            }

            if (canExecuteMethod == null)
            {
                return true;
            }

            return canExecuteMethod(parameter);
        }

        protected IDisposable StartExecuting()
        {
            isExecuting = true;
            RaiseCanExecuteChanged();

            return Disposable.Create(() =>
            {
                isExecuting = false;
                RaiseCanExecuteChanged();
            });
        }

        readonly Func<T, bool> canExecuteMethod;
        bool isExecuting;
    }
}