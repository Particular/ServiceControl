using System.Diagnostics;

static class FailedMessageIdGenerator
{
    public const string CollectionName = "FailedMessages";

    //[Obsolete("Use Guid.Parse")] TODO: As these are all guids... we don't need these generators.. Unless these are MessageIdentifiers as these can have any string value
    public static string MakeDocumentId(string messageUniqueId)
    {
        Debug.Assert(!HasPrefix(messageUniqueId), $"value has {CollectionName}/ prefix"); // TODO: Could potentially be removed when all tests are green but no harm as its only included on Debug builds
        return $"{CollectionName}/{messageUniqueId}";
    }

    public static string GetMessageIdFromDocumentId(string failedMessageDocumentId) => failedMessageDocumentId.Substring(CollectionName.Length + 1);
    static bool HasPrefix(string value) => value.StartsWith(CollectionName + "/");
}