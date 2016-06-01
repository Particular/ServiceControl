using System;

namespace ServiceControl.MessageRedirects
{
    public class MessageRedirect
    {
        private const string DocumentIdNamespace = "redirects/";

        public string Id { get; set; }

        public string MatchMessageType { get; set; }

        public string MatchSourceEndpoint { get; set; }
        public string RedirectToEndpoint { get; set; }
        public DateTime AsOfDateTime { get; set; }
        public DateTime ExpiresDateTime { get; set; }
        public long LastModified { get; set; }

        public static string GetDocumentIdFromMessageRedirectId(Guid messageRedirectId)
        {
            return DocumentIdNamespace + messageRedirectId;
        }

        public static Guid GetMessageRedirectIdFromDocumentId(string documentId)
        {
            return Guid.Parse(documentId.Replace(DocumentIdNamespace,string.Empty));
        }
    }
}
