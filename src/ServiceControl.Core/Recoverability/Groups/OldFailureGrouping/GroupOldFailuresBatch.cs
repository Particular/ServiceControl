namespace ServiceControl.Recoverability.Groups
{
    using System.Collections.Generic;

    public class GroupOldFailureBatch
    {
        public static string MakeDocumentId(string id)
        {
            return "GroupOldFailureBatches/" + id;
        }
        public string Id { get; set; }
        public List<List<string>> Failures{ get; set; }

        public bool ContainsMoreBatches()
        {
            return Failures != null && Failures.Count > 0;
        }

        public List<string> ConsumeBatch()
        {
            if (ContainsMoreBatches() == false)
                return null;

            var result = Failures[0];
            Failures.RemoveAt(0);
            return result;
        }
    }
}