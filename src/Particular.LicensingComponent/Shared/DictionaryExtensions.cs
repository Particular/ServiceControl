namespace Particular.LicensingComponent.Shared
{
    static class DictionaryExtensions
    {
        public static Dictionary<string, string> AddOrUpdate(this Dictionary<string, string> dictionary, string key, string value)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = value;
            }
            else
            {
                dictionary.Add(key, value);
            }

            return dictionary;
        }
    }
}
