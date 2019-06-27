namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class LockedHeaderModificationValidator
    {
        public bool Check(string[] lockedHeaders, IList<KeyValuePair<string, string>> editedMessageHeaders, Dictionary<string, string> originalMessageHeaders)
        {
            foreach (var header in lockedHeaders)
            {
                var originalHeader = originalMessageHeaders.FirstOrDefault(x => string.Compare(x.Key, header, StringComparison.InvariantCulture) == 0);
                var editedHeader = editedMessageHeaders.FirstOrDefault(x => string.Compare(x.Key, header, StringComparison.InvariantCulture) == 0);

                if (string.IsNullOrEmpty(originalHeader.Key) && !string.IsNullOrEmpty(editedHeader.Key))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(originalHeader.Key) && string.IsNullOrEmpty(editedHeader.Key))
                {
                    continue;
                }

                if (string.Compare(originalHeader.Value, editedHeader.Value, StringComparison.InvariantCulture) != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}