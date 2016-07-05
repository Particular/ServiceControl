using System;

namespace ServiceControl.MessageRedirects
{
    public class MessageRedirect
    {
        private const string DocumentIdNamespace = "redirects/";

        public string Id { get; set; }

        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }

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
