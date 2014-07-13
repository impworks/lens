using System;

namespace Lens.Resolver
{
	/// <summary>
	/// Provides common information about a type defined in the generated assembly.
	/// Is used for types, records, generic parameters and internal classes.
	/// </summary>
	internal class TypeDetails
	{
		public TypeDetails(Type type, Type baseType = null, Type[] interfaces = null, bool defCtor = false, bool byRef = false, bool byVal = false, bool genParam = false)
		{
			Type = type;
			BaseType = baseType ?? typeof (object);
			Interfaces = interfaces ?? Type.EmptyTypes;

			HasDefaultConstructor = defCtor;
			HasByRefRestriction = byRef;
			HasByValueRestriction = byVal;
			IsGenericParameter = genParam;
		}

		public Type Type;
		public Type BaseType;
		public Type[] Interfaces;
		public bool HasDefaultConstructor;
		public bool HasByRefRestriction;
		public bool HasByValueRestriction;
		public bool IsGenericParameter;
	}
}
