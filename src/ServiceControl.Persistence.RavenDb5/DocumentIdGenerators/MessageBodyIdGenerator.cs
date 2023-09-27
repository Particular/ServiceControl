static class MessageBodyIdGenerator
{
    const string CollectionName = "MessageBodies";

    public static string MakeDocumentId(string messageUniqueId)
    {
        return $"{CollectionName}/{messageUniqueId}";
    }
}