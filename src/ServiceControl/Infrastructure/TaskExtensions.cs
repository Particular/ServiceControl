namespace ServiceControl.Infrastructure
{
    using System.Threading.Tasks;

    public static class TaskEx
    {
        public static Task CompletedTask = Task.FromResult(0);
    }
}