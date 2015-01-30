namespace Particular.Backend.Debugging
{
    using System.Collections.Generic;

    public class SnapshotMetadata
    {
        readonly Dictionary<string, object> store;

        public SnapshotMetadata(Dictionary<string, object> store)
        {
            this.store = store;
        }

        public void Set(string key, object value)
        {
            store[key] = value;
        }
    }
}