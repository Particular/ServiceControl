using ServiceControl.MessageAuditing;

public static class MessageExtensions
{
    public static void MakeSystemMessage(this ProcessedMessage message, bool isSystem = true)
    {
        message.MessageMetadata["IsSystemMessage"] = isSystem;
    }

    public static void SetMessageId(this ProcessedMessage message, string messageId)
    {
        message.MessageMetadata["MessageId"] = messageId;
    }
}