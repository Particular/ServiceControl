namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System.IO;

    public class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public string Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            return $"/messages/{bodyId}/body";
        }

        public bool TryFetch(string bodyId, out Stream stream)
        {
            
                stream = null;
                return false;
            
            
        }
    }
}