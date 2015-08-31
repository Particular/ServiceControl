using System;
using System.Collections.Generic;

namespace ServiceControl.Config
{
    internal static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueFactory)
            where TKey : class
        {
            if (dictionary == null) throw new ArgumentNullException("dictionary");
            if (key == null) throw new ArgumentNullException("key");
            if (valueFactory == null) throw new ArgumentNullException("valueFactory");

            TValue value;
            if (dictionary.TryGetValue(key, out value))
                return value;

            value = valueFactory(key);
            dictionary.Add(key, value);
            return value;
        }

        public static TValue AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
            where TKey : class
        {
            if (dictionary == null) throw new ArgumentNullException("dictionary");
            if (key == null) throw new ArgumentNullException("key");
            if (addValueFactory == null) throw new ArgumentNullException("addValueFactory");
            if (updateValueFactory == null) throw new ArgumentNullException("updateValueFactory");

            TValue value;
            if (dictionary.TryGetValue(key, out value))
            {
                TValue updatedValue = updateValueFactory(key, value);
                dictionary[key] = updatedValue;
                return updatedValue;
            }

            value = addValueFactory(key);
            dictionary.Add(key, value);
            return value;
        }
    }
}