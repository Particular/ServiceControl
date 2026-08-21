namespace ServiceControl.Infrastructure.Ingestion;

using System.Threading.Tasks;
using NServiceBus.Transport;

public static class MessageContextExtensions
{
    public static TaskCompletionSource<bool> GetTaskCompletionSource(this MessageContext context) => context.Extensions.Get<TaskCompletionSource<bool>>(TaskCompletionSourceKey);

    public static void SetTaskCompletionSource(this MessageContext context, TaskCompletionSource<bool> value) => context.Extensions.Set(TaskCompletionSourceKey, value);

    // The bag belongs to a single message, so one key serves every ingestion in the process.
    const string TaskCompletionSourceKey = "ServiceControl.TaskCompletionSource";
}