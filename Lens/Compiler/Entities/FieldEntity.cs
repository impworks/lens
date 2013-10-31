using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.Compiler.Entities
{
	/// <summary>
	/// Represents a field defined in the generated assembly.
	/// </summary>
	internal class FieldEntity : TypeContentsBase
	{
		#region Fields

		/// <summary>
		/// Flag indicating the field belongs to the type, not its instances.
		/// </summary>
		public bool IsStatic;

		/// <summary>
		/// A string representation of the field's 
		/// </summary>
		public TypeSignature TypeSignature;

		/// <summary>
		/// Type of the values that can be saved in the field.
		/// </summary>
		public Type Type;

		/// <summary>
		/// Assembly-level field builder.
		/// </summary>
		public FieldBuilder FieldBuilder { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Creates a FieldBuilder for current field entity.
		/// </summary>
		public override void PrepareSelf()
		{
			if (FieldBuilder != null)
				return;

			var attrs = FieldAttributes.Public;
			if(IsStatic)
				attrs |= FieldAttributes.Static;

			if(Type == null)
				Type = ContainerType.Context.ResolveType(TypeSignature);

			FieldBuilder = ContainerType.TypeBuilder.DefineField(Name, Type, attrs);
		}

		#endregion
	}
}
