using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a decimal floating point literal.
	/// </summary>
	internal class DecimalNode : LiteralNodeBase<decimal>
	{
		public DecimalNode(decimal value = 0)
		{
			Value = value;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (int.MaxValue >= Value && int.MinValue <= Value)
			{
				var ctor = typeof (Decimal).GetConstructor(new[] { typeof (int) });
				gen.EmitConstant((int)Value);
				gen.EmitCreateObject(ctor);
			}
			else
			{
				var bits = Decimal.GetBits(Value);
				var ctor = typeof(Decimal).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte) });
				var sign = Value < Decimal.Zero;
				var scale = (bits[3] >> 16) & 0xFF;

				gen.EmitConstant(bits[0]);
				gen.EmitConstant(bits[1]);
				gen.EmitConstant(bits[2]);
				gen.EmitConstant(sign);
				gen.EmitConstant((byte)scale);
				gen.EmitCreateObject(ctor);
			}
		}
	}
}
