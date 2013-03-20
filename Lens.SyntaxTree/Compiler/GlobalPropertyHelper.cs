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
			m_Properties = new List<GlobalPropertyEntity>();
			m_Lookup = new Dictionary<string, int>();
		}

		private static readonly Dictionary<string, int> m_Lookup;
		private static readonly List<GlobalPropertyEntity> m_Properties;

		/// <summary>
		/// Removes all properties from the list.
		/// </summary>
		public static void Clear()
		{
			m_Properties.Clear();
			m_Lookup.Clear();
		}

		/// <summary>
		/// Gets property id by name.
		/// </summary>
		public static int FindByName(string name)
		{
			int id;
			if(!m_Lookup.TryGetValue(name, out id))
				throw new KeyNotFoundException();

			return id;
		}

		/// <summary>
		/// Registers a property and returns its unique ID.
		/// </summary>
		public static int RegisterProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			if(m_Lookup.ContainsKey(name))
				throw new ArgumentException(string.Format("Property '{0}' has already been defined!", name));

			var pty = new GlobalPropertyEntity(typeof (T), getter, setter);
			m_Properties.Add(pty);
			var id = m_Properties.Count - 1;
			m_Lookup.Add(name, id);
			return id;
		}

		/// <summary>
		/// Checks if the property has a getter.
		/// </summary>
		public static bool HasGetter(int id)
		{
			validateId(id);
			return m_Properties[id].Getter != null;
		}

		/// <summary>
		/// Checks if the property has a setter.
		/// </summary>
		public static bool HasSetter(int id)
		{
			validateId(id);
			return m_Properties[id].Setter != null;
		}

		/// <summary>
		/// Returns the type of the property.
		/// </summary>
		public static Type TypeOf(int id)
		{
			validateId(id);
			return m_Properties[id].PropertyType;
		}

		/// <summary>
		/// Gets the value of a property.
		/// </summary>
		public static T Get<T>(int id)
		{
			validateId(id);
			var info = m_Properties[id];

#if DEBUG
			if(typeof(T) != info.PropertyType)
				throw new InvalidOperationException(string.Format("Property #{0} is of type '{1}', but requested type was '{2}'.", id, info.PropertyType, typeof(T)));

			if(info.Getter == null)
				throw new InvalidOperationException(string.Format("Property #{0} has no getter!", id));
#endif

			return (info.Getter as Func<T>).Invoke();
		}

		/// <summary>
		/// Sets the value of a property.
		/// </summary>
		public static void Set<T>(int id, T value)
		{
			validateId(id);
			var info = m_Properties[id];

#if DEBUG
			if (typeof(T) != info.PropertyType)
				throw new InvalidOperationException(string.Format("Property #{0} is of type '{1}', but passed type was '{2}'.", id, info.PropertyType, typeof(T)));

			if (info.Setter == null)
				throw new InvalidOperationException(string.Format("Property #{0} has no setter!", id));
#endif

			(info.Setter as Action<T>).Invoke(value);
		}

		[Conditional("DEBUG")]
		private static void validateId(int id)
		{
			if(id < 0 || id > m_Properties.Count - 1)
				throw new ArgumentException(string.Format("Property #{0} has does not exist!", id));
		}

		private class GlobalPropertyEntity
		{
			public readonly Type PropertyType;
			public readonly Delegate Getter;
			public readonly Delegate Setter;

			public GlobalPropertyEntity(Type propType, Delegate getter, Delegate setter)
			{
				PropertyType = propType;
				Getter = getter;
				Setter = setter;
			}
		}
	}
}
