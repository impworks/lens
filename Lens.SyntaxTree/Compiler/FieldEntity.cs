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

		public bool IsStatic { get; set; }

		public Type Type { get; set; }

		public FieldBuilder FieldBuilder { get; private set; }

		#endregion

		#region Methods

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
