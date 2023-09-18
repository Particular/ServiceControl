static class MessageBodyIdGenerator
{
    const string CollectionName = "messagebodies";

    public static string MakeDocumentId(string messageUniqueId)
    {
        return $"{CollectionName}/{messageUniqueId}";
    }
}