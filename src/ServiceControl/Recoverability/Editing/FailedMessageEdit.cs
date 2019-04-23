namespace ServiceControl.Recoverability.Editing
{
    class FailedMessageEdit
    {
        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string EditId { get; set; }

        public static string MakeDocumentId(string failedMessageId)
        {
            return $"{CollectionName}/{failedMessageId}";
        }

        const string CollectionName = "FailedMessageEdit";
    }
}