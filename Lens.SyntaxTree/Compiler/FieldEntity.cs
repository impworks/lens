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

		public TypeSignature TypeSignature { get; set; }

		public Type Type { get; private set; }

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

			Type = ctx.ResolveType(TypeSignature.Signature);
			FieldBuilder = ContainerType.TypeBuilder.DefineField(Name, Type, attrs);
			_IsPrepared = true;
		}

		#endregion
	}
}
