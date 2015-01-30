namespace Particular.Operations.Ingestion.Api
{
    using System.Collections;
    using System.Collections.Generic;

    public class HeaderCollection : IEnumerable<KeyValuePair<string, string>>
    {
        readonly Dictionary<string, string> headers;

        public HeaderCollection(Dictionary<string, string> headers)
        {
            this.headers = headers;
        }

        public bool TryGet(string name, out string value)
        {
            return headers.TryGetValue(name, out value);
        }

        public string GetOrDefault(string name, string defaultValue = null)
        {
            string value;
            if (headers.TryGetValue(name, out value))
            {
                return value;
            }
            return defaultValue;
        }

        public bool Has(string name)
        {
            return headers.ContainsKey(name);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return headers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>(headers);
        }
    }
}