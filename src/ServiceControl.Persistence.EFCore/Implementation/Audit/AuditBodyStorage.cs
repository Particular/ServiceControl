namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

/// <summary>
/// Audit bodies are keyed by the row's ingestion hour and then its unique message id, so retention
/// can drop an hour's bodies with one prefix delete per store instead of one delete per message.
/// </summary>
public static class AuditBodyStorage
{
    public static string BodyId(DateTime createdOn, Guid uniqueMessageId) => $"{Prefix(createdOn)}{uniqueMessageId}";

    public static string Prefix(DateTime createdOn) => $"audit/{createdOn:yyyy-MM-dd-HH}/";
}
