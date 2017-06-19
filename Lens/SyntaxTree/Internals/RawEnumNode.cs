using System;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
	internal class RawEnumNode : NodeBase
	{
		#region Fields

		/// <summary>
		/// The enum type.
		/// </summary>
		public Type EnumType;

		/// <summary>
		/// The actual value of the enum.
		/// </summary>
		public long Value;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			return EnumType;
		}

		#endregion

		#region Emit

		protected override void EmitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if(Enum.GetUnderlyingType(EnumType).IsAnyOf(typeof(long), typeof(ulong)))
				gen.EmitConstant(Value);
			else
				gen.EmitConstant((int)Value);
		}

		#endregion
	}
}
