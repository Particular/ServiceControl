using System.Threading.Tasks;

namespace ServiceControl.Config.Framework.Commands
{
    public interface IAsyncCommand : IAsyncCommand<object>
    {
    }

    public interface IAsyncCommand<in T> : IRaiseCanExecuteChanged, System.Windows.Input.ICommand
    {
        Task ExecuteAsync(T obj);

        bool CanExecute(T obj);
    }
}