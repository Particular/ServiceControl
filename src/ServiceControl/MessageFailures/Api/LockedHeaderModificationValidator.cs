namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;

    public class LockedHeaderModificationValidator
    {
        public bool Check(string[] lockedHeaders, Dictionary<string, string> editedMessageHeaders, Dictionary<string, string> originalMessageHeaders)
        {
            foreach (var header in lockedHeaders)
            {
                originalMessageHeaders.TryGetValue(header, out var originalHeader);
                editedMessageHeaders.TryGetValue(header, out var editedHeader);
                //var editedHeader = editedMessageHeaders.FirstOrDefault(x => string.Compare(x.Key, header, StringComparison.InvariantCulture) == 0);

                if (string.IsNullOrEmpty(originalHeader) && !string.IsNullOrEmpty(editedHeader))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(originalHeader) && string.IsNullOrEmpty(editedHeader))
                {
                    continue;
                }

                if (string.Compare(originalHeader, editedHeader, StringComparison.InvariantCulture) != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}