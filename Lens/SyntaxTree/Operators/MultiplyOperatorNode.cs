using System;
using System.Text;
using Lens.Compiler;
using Lens.Translations;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that multiplies one value by another value.
	/// </summary>
	internal class MultiplyOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "*"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Multiply"; }
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			var type = Resolve(ctx);
			if (type.IsNumericType())
				return mathExpansion(LeftOperand, RightOperand) ?? mathExpansion(RightOperand, LeftOperand);

			if (!IsConstant)
			{ 
				if (type == typeof (string))
					return stringRepeatExpand(ctx);
			}

			return null;
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			// string repetition
			if (leftType == typeof(string) && rightType.IsNumericType())
				return typeof (string);

			// array repetition

			// sequence repetition

			return null;
		}

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitMultiply();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			var leftType = left.GetType();
			var rightType = right.GetType();

			if (leftType == typeof (string) && TypeExtensions.IsNumericType(rightType))
			{
				var sb = new StringBuilder();
				for (var idx = 0; idx < right; idx++)
					sb.Append(left);
				return sb.ToString();
			}

			try
			{
				return checked(left * right);
			}
			catch (OverflowException)
			{
				error(CompilerMessages.ConstantOverflow);
				return null;
			}
		}

		#region Expansion rules
		
		private static NodeBase mathExpansion(NodeBase one, NodeBase other)
		{
			if (one.IsConstant)
			{
				var value = one.ConstantValue;
				if (value == 0) return Expr.Int(0);
				if (value == 1) return other;
			}

			return null;
		}

		private NodeBase stringRepeatExpand(Context ctx)
		{
			var tmpString = ctx.Scope.DeclareImplicit(ctx, typeof(string), false);
			var tmpSb = ctx.Scope.DeclareImplicit(ctx, typeof (StringBuilder), false);
			var tmpIdx = ctx.Scope.DeclareImplicit(ctx, RightOperand.Resolve(ctx), false);

			// var sb = new StringBuilder();
			// for _ in 1..N do
			//    sb.Append (str)
			// str.ToString ()
			return Expr.Block(
				Expr.Let(tmpString, LeftOperand),
				Expr.Let(tmpSb, Expr.New(typeof(StringBuilder))),
				Expr.For(
					tmpIdx,
					Expr.Int(1),
					RightOperand,
					Expr.Block(
						Expr.Invoke(
							Expr.Get(tmpSb),
							"Append",
							Expr.Get(tmpString)
						)
					)
				),
				Expr.Invoke(
					Expr.Get(tmpSb),
					"ToString"
				)
			);
		}

		#endregion
	}
}
