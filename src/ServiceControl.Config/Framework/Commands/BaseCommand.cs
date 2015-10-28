using System;
using System.Reactive.Disposables;
using System.Windows.Input;

namespace ServiceControl.Config.Framework.Commands
{
    internal abstract class BaseCommand<T> : IRaiseCanExecuteChanged
    {
        private readonly Func<T, bool> canExecuteMethod;
        private bool isExecuting;

        public BaseCommand(Func<T, bool> canExecuteMethod = null)
        {
            this.canExecuteMethod = canExecuteMethod;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public bool CanExecute(T parameter)
        {
            if (isExecuting)
                return false;
            if (canExecuteMethod == null)
                return true;

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
    }
}