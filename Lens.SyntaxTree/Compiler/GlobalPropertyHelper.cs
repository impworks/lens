using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A class that lets LENS code invoke delegates from 
	/// </summary>
	public static class GlobalPropertyHelper
	{
		static GlobalPropertyHelper()
		{
			m_Properties = new List<List<Tuple<Delegate, Delegate>>>();
		}

		private static readonly List<List<Tuple<Delegate, Delegate>>> m_Properties;

		/// <summary>
		/// Adds a new tier for current compiler instance and returns the unique id.
		/// </summary>
		public static int RegisterContext()
		{
			m_Properties.Add(new List<Tuple<Delegate, Delegate>>());
			return m_Properties.Count - 1;
		}

		public static void UnregisterContext(int contextId)
		{
			if (contextId < 0 || contextId > m_Properties.Count - 1)
				throw new ArgumentException(string.Format("Context #{0} does not exist!", contextId));

#if DEBUG
			var curr = m_Properties[contextId];
			if (curr == null)
				throw new InvalidOperationException(string.Format("Context #{0} has been unregistered!", contextId));
#endif

			m_Properties[contextId] = null;
		}

		/// <summary>
		/// Registers a property and returns its unique ID.
		/// </summary>
		public static GlobalPropertyEntity RegisterProperty<T>(int contextId, Func<T> getter, Action<T> setter = null)
		{
			m_Properties[contextId].Add(new Tuple<Delegate, Delegate>(getter, setter));
			var id = m_Properties[contextId].Count - 1;

			return new GlobalPropertyEntity(id, typeof(T), getter != null, setter != null);
		}

		/// <summary>
		/// Gets the value of a property.
		/// </summary>
		public static T Get<T>(int contextId, int id)
		{
			validateId(contextId, id);
			var info = m_Properties[contextId][id];

#if DEBUG
			if(info.Item1 == null)
				throw new InvalidOperationException(string.Format("Property #{0} has no getter!", id));
#endif

			return (info.Item1 as Func<T>).Invoke();
		}

		/// <summary>
		/// Sets the value of a property.
		/// </summary>
		public static void Set<T>(int contextId, int id, T value)
		{
			validateId(contextId, id);
			var info = m_Properties[contextId][id];

#if DEBUG
			if (info.Item2 == null)
				throw new InvalidOperationException(string.Format("Property #{0} has no setter!", id));
#endif

			(info.Item2 as Action<T>).Invoke(value);
		}

		[Conditional("DEBUG")]
		private static void validateId(int contextId, int id)
		{
			if (contextId < 0 || contextId > m_Properties.Count - 1)
				throw new ArgumentException(string.Format("Context #{0} does not exist!", contextId));

			var curr = m_Properties[contextId];
			if(curr == null)
				throw new InvalidOperationException(string.Format("Context #{0} has been unregistered!", contextId));

			if(id < 0 || id > m_Properties[contextId].Count - 1)
				throw new ArgumentException(string.Format("Property #{0} does not exist!", id));
		}
	}

	public class GlobalPropertyEntity
	{
		public readonly int PropertyId;
		public readonly Type PropertyType;
		public readonly bool HasGetter;
		public readonly bool HasSetter;

		public GlobalPropertyEntity(int id, Type propType, bool hasGetter, bool hasSetter)
		{
			PropertyId = id;
			PropertyType = propType;
			HasGetter = hasGetter;
			HasSetter = hasSetter;
		}
	}
}
