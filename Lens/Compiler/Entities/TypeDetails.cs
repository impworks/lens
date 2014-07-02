using System;

namespace Lens.Compiler.Entities
{
	/// <summary>
	/// Provides common information about a type defined in the generated assembly.
	/// Is used for types, records, generic parameters and internal classes.
	/// </summary>
	internal class TypeDetails
	{
		public TypeDetails(Type type, Type baseType, Type[] interfaces)
		{
			Type = type;
			BaseType = baseType;
			Interfaces = interfaces;
		}

		public readonly Type Type;
		public readonly Type BaseType;
		public readonly Type[] Interfaces;
	}
}
