namespace ServiceControl.Config.Framework.Commands
{
    public interface ICommand : ICommand<object>
    {
    }

    public interface ICommand<in T> : IRaiseCanExecuteChanged, System.Windows.Input.ICommand
    {
        void Execute(T obj);

        bool CanExecute(T obj);
    }
}