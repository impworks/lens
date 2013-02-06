using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
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
		public bool IsStatic { get; set; }

		/// <summary>
		/// Type of the values that can be saved in the field.
		/// </summary>
		public Type Type { get; set; }

		/// <summary>
		/// Assembly-level field builder.
		/// </summary>
		public FieldBuilder FieldBuilder { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Creates a FieldBuilder for current field entity.
		/// </summary>
		/// <param name="ctx"></param>
		public override void PrepareSelf(Context ctx)
		{
			if (_IsPrepared)
				return;

			var attrs = FieldAttributes.Public;
			if(IsStatic)
				attrs |= FieldAttributes.Static;

			FieldBuilder = ContainerType.TypeBuilder.DefineField(Name, Type, attrs);
			_IsPrepared = true;
		}

		#endregion
	}
}
