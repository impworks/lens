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
			_Data = new Dictionary<string, T>();
			_Keys = new List<string>();
		}

		public HashList(IEnumerable<T> src, Func<T, string> nameGetter) : this()
		{
			if(src != null)
				foreach (var curr in src)
					Add(nameGetter(curr), curr);
		}

		#endregion

		#region Fields

		private readonly Dictionary<string, T> _Data;
		private readonly List<string> _Keys;

		public IEnumerable<string> Keys { get { return _Keys.OfType<string>(); } }
		public IEnumerable<T> Values { get { return _Keys.Select(curr => _Data[curr]); } }

		#endregion

		#region Methods

		/// <summary>
		/// Adds an item to the collection.
		/// </summary>
		public void Add(string key, T value)
		{
			_Data.Add(key, value);
			_Keys.Add(key);
		}

		/// <summary>
		/// Removes everything from the collection.
		/// </summary>
		public void Clear()
		{
			_Data.Clear();
			_Keys.Clear();
		}

		/// <summary>
		/// Checks if a key exists.
		/// </summary>
		public bool Contains(string key)
		{
			return _Data.ContainsKey(key);
		}

		/// <summary>
		/// Gets an item by string key.
		/// </summary>
		public T this[string key]
		{
			get { return _Data[key]; }
			set { _Data[key] = value; }
		}

		/// <summary>
		/// Get an item by integer index.
		/// </summary>
		public T this[int id]
		{
			get { return _Data[_Keys[id]]; }
			set { _Data[_Keys[id]] = value; }
		}

		/// <summary>
		/// Proxied count.
		/// </summary>
		public int Count
		{
			get { return _Keys.Count; }
		}

		/// <summary>
		/// Gets index of key.
		/// </summary>
		public int IndexOf(string key)
		{
			return _Keys.IndexOf(key);
		}

		#endregion

		#region IEnumerable<T> implementation

		/// <summary>
		/// Proxied enumerator.
		/// </summary>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return _Keys.GetEnumerator();
		}

		public IEnumerator<string> GetEnumerator()
		{
			return _Keys.GetEnumerator();
		}

		#endregion
	}
}
