using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that adds two values together.
	/// </summary>
	internal class AddOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "+"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Addition"; }
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				var type = Resolve(ctx);

				if (type == typeof (string))
					return stringExpand();

				if (type.IsArray)
					return arrayExpand(ctx);

				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (IEnumerable<>))
					return seqExpand();
			}

			return mathExpand(LeftOperand, RightOperand) ?? mathExpand(RightOperand, LeftOperand);
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			if(leftType == typeof (string) && rightType == typeof (string))
				return typeof (string);

			if (leftType == rightType && leftType.IsArray)
				return leftType;

			var leftEnumerable = leftType.ResolveImplementationOf(typeof(IEnumerable<>));
			var rightEnumerable = rightType.ResolveImplementationOf(typeof(IEnumerable<>));
			if (leftEnumerable == rightEnumerable && leftEnumerable != null)
				return leftEnumerable;

			return null;
		}

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitAdd();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			try
			{
				return checked(left + right);
			}
			catch (OverflowException)
			{
				error(CompilerMessages.ConstantOverflow);
				return null;
			}
		}

		#region Expands
		
		/// <summary>
		/// Returns the code to expand mathematic operations if available.
		/// </summary>
		private static NodeBase mathExpand(NodeBase one, NodeBase other)
		{
			if (one.IsConstant)
			{
				var value = one.ConstantValue;
				if(TypeExtensions.IsNumericType(value.GetType()) && value == 0)
					return other;
			}

			return null;
		}

		/// <summary>
		/// Returns the code to concatenate two strings.
		/// </summary>
		private NodeBase stringExpand()
		{
			return Expr.Invoke("string", "Concat", LeftOperand, RightOperand);
		}

		/// <summary>
		/// Returns the code to concatenate two arrays.
		/// </summary>
		private NodeBase arrayExpand(Context ctx)
		{
			var type = Resolve(ctx);

			var tmpArray = ctx.Scope.DeclareImplicit(ctx, type, false);
			var tmpLeft = ctx.Scope.DeclareImplicit(ctx, type, false);
			var tmpRight = ctx.Scope.DeclareImplicit(ctx, type, false);

			return Expr.Block(
				Expr.Set(tmpLeft, LeftOperand),
				Expr.Set(tmpRight, RightOperand),
				Expr.Set(
					tmpArray,
					Expr.Array(
						type.GetElementType(),
						Expr.Add(
							Expr.GetMember(Expr.Get(tmpLeft), "Length"),
							Expr.GetMember(Expr.Get(tmpRight), "Length")
						)
					)
				),
				Expr.Invoke(
					"System.Array",
					"Copy",
					Expr.Get(tmpLeft),
					Expr.Get(tmpArray),
					Expr.GetMember(Expr.Get(tmpLeft), "Length")
				),
				Expr.Invoke(
					"System.Array",
					"Copy",
					Expr.Get(tmpRight),
					Expr.Int(0),
					Expr.Get(tmpArray),
					Expr.GetMember(Expr.Get(tmpLeft), "Length"),
					Expr.GetMember(Expr.Get(tmpRight), "Length")
				),
				Expr.Get(tmpArray)
			);
		}

		/// <summary>
		/// Returns the code to concatenate two value sequences.
		/// </summary>
		private NodeBase seqExpand()
		{
			return Expr.Invoke("System.Linq.Enumerable", "Concat", LeftOperand, RightOperand);
		}

		#endregion
	}
}
