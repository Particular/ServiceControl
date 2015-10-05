using System.Threading.Tasks;

namespace ServiceControl.Config.Framework.Commands
{
    internal abstract class AwaitableAbstractCommand<T> : BaseCommand<T>, ICommand<T>
    {
        public abstract Task ExecuteAsync(T obj);

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
    }
}