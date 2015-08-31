using ServiceControl.Config.Framework.Commands;

namespace ServiceControl.Config.Extensions
{
    internal static class CommandExtensions
    {
        public static void RaiseCanExecuteChanged(this System.Windows.Input.ICommand command)
        {
            var canExecuteChanged = command as IRaiseCanExecuteChanged;

            if (canExecuteChanged != null)
                canExecuteChanged.RaiseCanExecuteChanged();
        }
    }
}