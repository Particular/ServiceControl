namespace ServiceControlInstaller.Engine.ReportCard
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    class TruncatedStringList : IList<String>
    {
        public TruncatedStringList(int maxLengthsOfItems)
        {
            if (maxLengthsOfItems < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLengthsOfItems), "Must greater than 0");
            }

            maxLength = maxLengthsOfItems;
        }

        public void Add(string item)
        {
            itemList.Add(Truncate(item));
        }

        public void Clear()
        {
            itemList.Clear();
        }

        public bool Contains(string item)
        {
            return itemList.Contains(item);
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            itemList.CopyTo(array, arrayIndex);
        }

        public bool Remove(string item)
        {
            return itemList.Remove(item);
        }

        public int Count => itemList.Count;


        public bool IsReadOnly => true;

        public IEnumerator<string> GetEnumerator()
        {
            return itemList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return itemList.GetEnumerator();
        }

        public int IndexOf(string item)
        {
            return itemList.IndexOf(item);
        }

        public void Insert(int index, string item)
        {
            itemList.Insert(index, Truncate(item));
        }

        public void RemoveAt(int index)
        {
            itemList.RemoveAt(index);
        }

        public string this[int index]
        {
            get { return itemList[index]; }
            set { itemList[index] = value; }
        }

        string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            var suffix = s.Length > maxLength ? "... " : String.Empty;
            return s.Substring(0, Math.Min(s.Length, maxLength)) + suffix;
        }

        public void AddRange(IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        List<string> itemList = new List<string>();

        int maxLength;
    }
}