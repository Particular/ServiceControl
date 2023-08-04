static class FailedMessageIdGenerator
{
    public const string CollectionName = "FailedMessages";

    //[Obsolete("Use Guid.Parse")] TODO: As these are all guids... we don't need these generators.. Unless these are MessageIdentifiers as these can have any string value
    public static string MakeDocumentId(string messageUniqueId) => $"{CollectionName}/{messageUniqueId}";
    public static string GetMessageIdFromDocumentId(string failedMessageDocumentId) => failedMessageDocumentId.Substring(CollectionName.Length + 1);
}