using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the operator that returns a default value for the type.
	/// </summary>
	public class DefaultOperatorNode : TypeOperatorNodeBase
	{
		private string _ValueTypeLocalVariableName;

		public DefaultOperatorNode(string type = null)
		{
			Type = type;
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			return ctx.ResolveType(Type.Signature);
		}

		public override void ProcessClosures(Context ctx)
		{
			// register a local variable for current node if it's a value type.
			_ValueTypeLocalVariableName = ctx.CurrentScope.DeclareImplicitName(GetExpressionType(ctx), false).Name;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var type = GetExpressionType(ctx);

			if(type == typeof(bool))
				gen.EmitConstant(false);
			else if(type == typeof(int))
				gen.EmitConstant(0);
			else if (type == typeof (long))
				gen.EmitConstant(0L);
			else if (type == typeof (float))
				gen.EmitConstant(0f);
			else if (type == typeof (double))
				gen.EmitConstant(0.0);
			else if(!type.IsValueType)
				gen.EmitNull();
			else
			{
				var id = ctx.CurrentScope.FindName(_ValueTypeLocalVariableName);
				gen.EmitLoadLocalAddress(id.LocalId.Value);
				gen.EmitInitObject(type);
			}
		}

		public override string ToString()
		{
			return string.Format("default({0})", Type);
		}
	}
}
