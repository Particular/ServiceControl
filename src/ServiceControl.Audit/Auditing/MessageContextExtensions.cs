namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;
    using NServiceBus.Transport;

    static class MessageContextExtensions
    {
        public static TaskCompletionSource<bool> GetTaskCompletionSource(this MessageContext context)
        {
            return context.Extensions.Get<TaskCompletionSource<bool>>(TaskCompletionSourceKey);
        }

        public static void SetTaskCompletionSource(this MessageContext context, TaskCompletionSource<bool> value)
        {
            context.Extensions.Set(TaskCompletionSourceKey, value);
        }

        const string TaskCompletionSourceKey = "ServiceControl.Audit.TaskCompletionSource";
    }
}