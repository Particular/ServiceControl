﻿static class FailedMessageIdGenerator
{
    public const string CollectionName = "FailedMessages";

    public static string MakeDocumentId(string messageUniqueId) => $"{CollectionName}/{messageUniqueId}";

    public static string GetMessageIdFromDocumentId(string failedMessageDocumentId) => failedMessageDocumentId.Substring(CollectionName.Length + 1);
}