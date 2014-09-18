using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lens.Compiler.Entities;
using Lens.Translations;

namespace Lens.Resolver
{
	/// <summary>
	/// A class that lets LENS code invoke delegates from the host application.
	/// </summary>
	public static class GlobalPropertyHelper
	{
		#region Constructor

		static GlobalPropertyHelper()
		{
			m_Properties = new List<List<GlobalPropertyEntity>>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// The total list of registered properties in ALL contexts.
		/// Values are NEVER removed: an unregistered context is replaced by a null value, therefore maintaining the sequence of context IDs.
		/// </summary>
		private static readonly List<List<GlobalPropertyEntity>> m_Properties;

		#endregion

		#region Methods

		/// <summary>
		/// Adds a new tier for current compiler instance and returns the unique id.
		/// </summary>
		public static int RegisterContext()
		{
			m_Properties.Add(new List<GlobalPropertyEntity>());
			return m_Properties.Count - 1;
		}

		/// <summary>
		/// Removes current context from the global list of registered properties.
		/// </summary>
		public static void UnregisterContext(int contextId)
		{
			if (contextId < 0 || contextId > m_Properties.Count - 1)
				throw new ArgumentException(string.Format(CompilerMessages.ContextNotFound, contextId));

#if DEBUG
			var curr = m_Properties[contextId];
			if (curr == null)
				throw new InvalidOperationException(string.Format(CompilerMessages.ContextUnregistered, contextId));
#endif

			m_Properties[contextId] = null;
		}

		/// <summary>
		/// Registers a strongly typed property and returns its unique ID.
		/// </summary>
		internal static GlobalPropertyInfo RegisterProperty<T>(int contextId, Func<T> getter, Action<T> setter = null)
		{
			if (getter == null && setter == null)
				throw new ArgumentNullException("getter");

			m_Properties[contextId].Add(new GlobalPropertyEntity { Getter = getter, Setter = setter } );
			var id = m_Properties[contextId].Count - 1;

			return new GlobalPropertyInfo(id, typeof(T), getter != null, setter != null, null, null);
		}

		/// <summary>
		/// Gets the value of a property.
		/// </summary>
		public static T Get<T>(int contextId, int id)
		{
			validateId(contextId, id);
			var info = m_Properties[contextId][id];

#if DEBUG
			if(info.Getter == null)
				throw new InvalidOperationException(string.Format(CompilerMessages.PropertyIdNoGetter, id));
#endif

			return (info.Getter as Func<T>).Invoke();
		}

		/// <summary>
		/// Sets the value of a property.
		/// </summary>
		public static void Set<T>(int contextId, int id, T value)
		{
			validateId(contextId, id);
			var info = m_Properties[contextId][id];

#if DEBUG
			if (info.Setter == null)
				throw new InvalidOperationException(string.Format(CompilerMessages.PropertyIdNoSetter, id));
#endif

			(info.Setter as Action<T>).Invoke(value);
		}

		#endregion

		#region Helpers

		[Conditional("DEBUG")]
		private static void validateId(int contextId, int id)
		{
			if (contextId < 0 || contextId > m_Properties.Count - 1)
				throw new ArgumentException(string.Format(CompilerMessages.ContextNotFound, contextId));

			var curr = m_Properties[contextId];
			if(curr == null)
				throw new InvalidOperationException(string.Format(CompilerMessages.ContextUnregistered, contextId));

			if(id < 0 || id > m_Properties[contextId].Count - 1)
				throw new ArgumentException(string.Format(CompilerMessages.PropertyIdNotFound, id));
		}

		private class GlobalPropertyEntity
		{
			public Delegate Getter;
			public Delegate Setter;
		}

		#endregion
	}

	/// <summary>
	/// Single entry of a registered property.
	/// </summary>
	internal class GlobalPropertyInfo
	{
		public readonly int PropertyId;
		public readonly Type PropertyType;

		public readonly bool HasGetter;
		public readonly bool HasSetter;

		public readonly MethodEntity GetterMethod;
		public readonly MethodEntity SetterMethod;

		public GlobalPropertyInfo(int id, Type propType, bool hasGetter, bool hasSetter, MethodEntity getterMethod, MethodEntity setterMethod)
		{
			PropertyId = id;
			PropertyType = propType;
			HasGetter = hasGetter;
			HasSetter = hasSetter;

			GetterMethod = getterMethod;
			SetterMethod = setterMethod;
		}
	}
}
