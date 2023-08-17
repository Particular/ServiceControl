static class MessageBodyIdGenerator
{
    const string CollectionName = "messagebodies";

    public static string MakeDocumentId(string messageUniqueId)
    {
        Guard.Assert(!HasPrefix(messageUniqueId), $"value has {CollectionName}/ prefix"); // TODO: Could potentially be removed when all tests are green but no harm as its only included on Debug builds
        return $"{CollectionName}/{messageUniqueId}";
    }

    static bool HasPrefix(string value) => value.StartsWith(CollectionName + "/");
}