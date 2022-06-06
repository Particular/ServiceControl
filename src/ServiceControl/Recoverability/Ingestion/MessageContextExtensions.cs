namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.Transport;

    static class MessageContextExtensions
    {
        public static TaskCompletionSource<bool> GetTaskCompletionSource(this MessageContext context) => context.Extensions.Get<TaskCompletionSource<bool>>(TaskCompletionSourceKey);

        public static void SetTaskCompletionSource(this MessageContext context, TaskCompletionSource<bool> value) => context.Extensions.Set(TaskCompletionSourceKey, value);

        const string TaskCompletionSourceKey = "ServiceControl.TaskCompletionSource";
    }
}