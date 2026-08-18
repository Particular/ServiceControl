namespace ServiceControl.Config.Framework.Commands
{
    using System.Threading.Tasks;

    public interface IAsyncCommand : IAsyncCommand<object>;

#pragma warning disable PS0018
    public interface IAsyncCommand<in T> : IRaiseCanExecuteChanged, System.Windows.Input.ICommand
    {
        Task ExecuteAsync(T obj);

        bool CanExecute(T obj);
    }
#pragma warning restore PS0018
}