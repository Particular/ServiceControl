namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;

    static class LockedHeaderModificationValidator
    {
        public static bool Check(string[] lockedHeaders, Dictionary<string, string> editedMessageHeaders, Dictionary<string, string> originalMessageHeaders)
        {
            foreach (var header in lockedHeaders)
            {
                originalMessageHeaders.TryGetValue(header, out var originalHeader);
                editedMessageHeaders.TryGetValue(header, out var editedHeader);

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