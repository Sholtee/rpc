/********************************************************************************
* NameValueCollectionWrapper.cs                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Solti.Utils.Rpc.Internals
{
    internal sealed class NameValueCollectionWrapper : IDictionary<string, string>, IReadOnlyDictionary<string, string>
    {
        public NameValueCollectionWrapper(NameValueCollection originalCollection) => OriginalCollection = originalCollection;

        public NameValueCollection OriginalCollection { get; }

        public string this[string key] { get => OriginalCollection[key]; set => OriginalCollection[key] = value; }

        public ICollection<string> Keys => OriginalCollection.AllKeys;

        public ICollection<string> Values => throw new NotImplementedException();

        public int Count => OriginalCollection.Count;

        public bool IsReadOnly { get; }

        IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => Keys;

        IEnumerable<string> IReadOnlyDictionary<string, string>.Values => Values;

        public void Add(string key, string value) => OriginalCollection.Add(key, value);

        public void Add(KeyValuePair<string, string> item) => OriginalCollection.Add(item.Key, item.Value);

        public void Clear() => OriginalCollection.Clear();

        public bool Contains(KeyValuePair<string, string> item) => OriginalCollection.GetValues(item.Key).Contains(item.Value);

        public bool ContainsKey(string key) => OriginalCollection[key] is not null;

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            foreach (KeyValuePair<string, string> kvp in this)
            {
                array[arrayIndex++] = kvp;
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => OriginalCollection
            .AllKeys
            .Select(key => new KeyValuePair<string, string>(key, OriginalCollection[key]))
            .GetEnumerator();

        public bool Remove(string key)
        {
            int oldCount = OriginalCollection.Count;
            OriginalCollection.Remove(key);
            return oldCount > OriginalCollection.Count;
        }

        public bool Remove(KeyValuePair<string, string> item) => throw new NotImplementedException();

        public bool TryGetValue(string key, out string value) => (value = OriginalCollection[key]) is not null || OriginalCollection.AllKeys.Contains(key);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
