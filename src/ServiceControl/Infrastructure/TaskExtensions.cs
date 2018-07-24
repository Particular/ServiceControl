namespace ServiceControl.Infrastructure
{
    using System.Threading.Tasks;

    public static class TaskEx
    {
        // ReSharper disable once UnusedParameter.Global
        // Used to explicitly suppress the compiler warning about
        // using the returned value from async operations
        public static void Ignore(this Task task)
        {
        }

        public static Task CompletedTask = Task.FromResult(0);
    }
}