using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Utils
{
	/// <summary>
	/// Dictionary, учитывающий также порядок добавления элементов.
	/// </summary>
	/// <typeparam name="T">Тип данных</typeparam>
	public class HashList<T> : IEnumerable<string>
	{
		private readonly Dictionary<string, T> _Data;
		private readonly List<string> _Keys;

		public HashList()
		{
			_Data = new Dictionary<string, T>();
			_Keys = new List<string>();
		}

		/// <summary>
		/// Add an item to the collection
		/// </summary>
		/// <param name="key">Key</param>
		/// <param name="value">Object</param>
		public void Add(string key, T value)
		{
			_Data.Add(key, value);
			_Keys.Add(key);
		}

		/// <summary>
		/// Remove everything from the collection
		/// </summary>
		public void Clear()
		{
			_Data.Clear();
			_Keys.Clear();
		}

		/// <summary>
		/// Check if a key exists
		/// </summary>
		/// <param name="key">Key</param>
		public bool Contains(string key)
		{
			return _Data.ContainsKey(key);
		}

		/// <summary>
		/// Get an item by string key
		/// </summary>
		/// <param name="key">String key</param>
		public T this[string key]
		{
			get { return _Data[key]; }
			set { _Data[key] = value; }
		}

		/// <summary>
		/// Get an item by integer index
		/// </summary>
		/// <param name="id">Integer index</param>
		public T this[int id]
		{
			get { return _Data[_Keys[id]]; }
			set { _Data[_Keys[id]] = value; }
		}

		/// <summary>
		/// Proxied count
		/// </summary>
		public int Count
		{
			get { return _Keys.Count; }
		}

		/// <summary>
		/// Proxied enumerator
		/// </summary>
		/// <returns></returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return _Keys.GetEnumerator();
		}

		public IEnumerator<string> GetEnumerator()
		{
			return _Keys.GetEnumerator();
		}

		public IEnumerable<string> Keys
		{
			get { return _Keys.OfType<string>(); }
		}

		public IEnumerable<T> Values
		{
			get { return _Keys.Select(curr => _Data[curr]); }
		}
	}
}
