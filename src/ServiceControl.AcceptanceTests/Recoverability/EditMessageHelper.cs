namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class EditMessageHelper
    {
        public static List<KeyValuePair<string, string>> TryRestoreOriginalHeaderKeys(Dictionary<string, string> headers)
        {
            // brings up the normal header key for most of the message headers. Unfortunately this approach can't fix all headers as during serialization some of the necessary information is lost.
            headers["SC.SessionID"] = headers["sc.session_id"];
            headers.Remove("sc.session_id");

            return headers.Select(header => new KeyValuePair<string, string>(FixKeyName(header.Key), header.Value)).ToList();

            string FixKeyName(string headerKey)
            {
                var newKey = Regex.Replace(headerKey, @"([0-9a-z])\.([a-z])", m => $"{m.Groups[1].Value}.{m.Groups[2].Value.ToUpper()}");
                newKey = Regex.Replace(newKey, "([0-9a-z])_([a-z])", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToUpper()}");
                return char.ToUpper(newKey[0]) + newKey.Substring(1);
            }
        }
    }
}