using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Utils
{
    /// <summary>
    /// A dictionary-like type that maintains item addition order.
    /// </summary>
    internal class HashList<T> : IEnumerable<string>
    {
        #region Constructors

        public HashList()
        {
            _data = new Dictionary<string, T>();
            _keys = new List<string>();
        }

        public HashList(IEnumerable<T> src, Func<T, string> nameGetter) : this()
        {
            if (src != null)
                foreach (var curr in src)
                    Add(nameGetter(curr), curr);
        }

        #endregion

        #region Fields

        private readonly Dictionary<string, T> _data;
        private readonly List<string> _keys;

        public IEnumerable<string> Keys => _keys.OfType<string>();

        public IEnumerable<T> Values
        {
            get { return _keys.Select(curr => _data[curr]); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        public void Add(string key, T value)
        {
            _data.Add(key, value);
            _keys.Add(key);
        }

        /// <summary>
        /// Removes everything from the collection.
        /// </summary>
        public void Clear()
        {
            _data.Clear();
            _keys.Clear();
        }

        /// <summary>
        /// Checks if a key exists.
        /// </summary>
        public bool Contains(string key)
        {
            return _data.ContainsKey(key);
        }

        /// <summary>
        /// Gets an item by string key.
        /// </summary>
        public T this[string key]
        {
            get => _data[key];
            set => _data[key] = value;
        }

        /// <summary>
        /// Get an item by integer index.
        /// </summary>
        public T this[int id]
        {
            get => _data[_keys[id]];
            set => _data[_keys[id]] = value;
        }

        /// <summary>
        /// Proxied count.
        /// </summary>
        public int Count => _keys.Count;

        /// <summary>
        /// Gets index of key.
        /// </summary>
        public int IndexOf(string key)
        {
            return _keys.IndexOf(key);
        }

        #endregion

        #region IEnumerable<T> implementation

        /// <summary>
        /// Proxied enumerator.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _keys.GetEnumerator();
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _keys.GetEnumerator();
        }

        #endregion
    }
}