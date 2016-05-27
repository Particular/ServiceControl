using System;

namespace ServiceControl.Config.Framework.Commands
{
    internal class DelegateCommand : DelegateCommand<object>, ICommand
    {
        public DelegateCommand(Action<object> executeMethod, Func<object, bool> canExecuteMethod = null) : base(executeMethod, canExecuteMethod)
        {
        }
    }

    internal class DelegateCommand<T> : BaseCommand<T>, ICommand<T>
    {
        private readonly Action<T> executeMethod;

        public DelegateCommand(Action<T> executeMethod, Func<T, bool> canExecuteMethod = null) : base(canExecuteMethod)
        {
            if (executeMethod == null)
                throw new ArgumentNullException(nameof(executeMethod), @"Execute Method cannot be null");

            this.executeMethod = executeMethod;
        }

        bool System.Windows.Input.ICommand.CanExecute(object parameter)
        {
            return CanExecute((T)parameter);
        }

        void System.Windows.Input.ICommand.Execute(object parameter)
        {
            Execute((T)parameter);
        }

        public void Execute(T parameter)
        {
            using (StartExecuting())
            {
                executeMethod(parameter);
            }
        }
    }
}