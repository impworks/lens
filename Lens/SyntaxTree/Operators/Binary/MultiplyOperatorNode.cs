using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// An operator node that multiplies one value by another value.
	/// </summary>
	internal class MultiplyOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "*"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Multiply"; }
		}

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			if (rightType == typeof(int))
			{
				// string repetition
				if (leftType == typeof(string))
					return typeof(string);

				// array repetition
				if (leftType.IsArray)
					return leftType;

				// typed sequence repetition
				var enumerable = leftType.ResolveImplementationOf(typeof(IEnumerable<>));
				if (enumerable != null)
					return enumerable;

				// untyped sequence repetition
				if (leftType.Implements(typeof(IEnumerable), false))
					return typeof(IEnumerable);
			}

			return null;
		}

		#endregion

		#region Transform

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				var type = Resolve(ctx);

				if (type == typeof (string))
					return stringExpand(ctx);

				if (type.IsArray)
					return arrayExpand(ctx);

				if (type == typeof (IEnumerable) || type.IsAppliedVersionOf(typeof (IEnumerable<>)))
					return seqExpand(ctx);
			}

			return base.expand(ctx, mustReturn);
		}

		/// <summary>
		/// Repeats a string.
		/// </summary>
		private NodeBase stringExpand(Context ctx)
		{
			var tmpString = ctx.Scope.DeclareImplicit(ctx, typeof(string), false);
			var tmpSb = ctx.Scope.DeclareImplicit(ctx, typeof(StringBuilder), false);
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

		/// <summary>
		/// Repeats an array.
		/// </summary>
		private NodeBase arrayExpand(Context ctx)
		{
			var arrayType = LeftOperand.Resolve(ctx);
			var tmpLeft = ctx.Scope.DeclareImplicit(ctx, arrayType, false);
			var tmpResult = ctx.Scope.DeclareImplicit(ctx, arrayType, false);
			var tmpRight = ctx.Scope.DeclareImplicit(ctx, typeof(int), false);
			var tmpIndex = ctx.Scope.DeclareImplicit(ctx, typeof(int), false);

			// a = <left>
			// b = right
			// result = new T[a.Length * b]
			// for idx in 0..a.Length-1 do
			//    Array::Copy(from: a; to: result; targetIndex: idx * a.Length)
			// result
			return Expr.Block(
				Expr.Set(tmpLeft, LeftOperand),
				Expr.Set(
					tmpRight,
					Expr.Invoke(
						"System.Math",
						"Abs",
						RightOperand
					)
				),
				Expr.Set(
					tmpResult,
					Expr.Array(
						arrayType.GetElementType(),
						Expr.Mult(
							Expr.GetMember(
								Expr.Get(tmpLeft),
								"Length"
							),
							Expr.Get(tmpRight)
						)
					)
				),
				Expr.For(
					tmpIndex,
					Expr.Int(0),
					Expr.Get(tmpRight),
					Expr.Block(
						Expr.Invoke(
							"System.Array",
							"Copy",
							Expr.Get(tmpLeft),
							Expr.Int(0),
							Expr.Get(tmpResult),
							Expr.Mult(
								Expr.Get(tmpIndex),
								Expr.GetMember(
									Expr.Get(tmpLeft),
									"Length"
								)
							),
							Expr.GetMember(
								Expr.Get(tmpLeft),
								"Length"
							)
						)
					)
				),
				Expr.Get(tmpResult)
			);
		}

		/// <summary>
		/// Repeats a typed or untyped sequence.
		/// </summary>
		private NodeBase seqExpand(Context ctx)
		{
			var seqType = LeftOperand.Resolve(ctx);

			NodeBase leftWrapper;
			if (seqType == typeof(IEnumerable))
			{
				leftWrapper = Expr.Invoke(Expr.GetMember("System.Linq.Enumerable", "OfType", "object"), LeftOperand);
				seqType = typeof(IEnumerable<object>);
			}
			else
			{
				leftWrapper = LeftOperand;
			}

			var tmpLeft = ctx.Scope.DeclareImplicit(ctx, seqType, false);
			var tmpResult = ctx.Scope.DeclareImplicit(ctx, seqType, false);
			var tmpIndex = ctx.Scope.DeclareImplicit(ctx, typeof(int), false);

			// a = <left>
			// result = a
			// for x in 1..(Math.Abs <right>) do
			//   result = result.Concat a
			// result
			return Expr.Block(
				Expr.Set(tmpLeft, leftWrapper),
				Expr.Set(tmpResult, Expr.Get(tmpLeft)),
				Expr.For(
					tmpIndex,
					Expr.Int(1),
					Expr.Invoke(
						"System.Math",
						"Abs",
						RightOperand
					),
					Expr.Block(
						Expr.Set(
							tmpResult,
							Expr.Invoke(
								"System.Linq.Enumerable",
								"Concat",
								Expr.Get(tmpResult),
								Expr.Get(tmpLeft)
							)
						)
					)
				),
				Expr.Get(tmpResult)
			);
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitMultiply();
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			var leftType = left.GetType();
			var rightType = right.GetType();

			if (leftType == typeof (string) && rightType == typeof(int))
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

		#endregion
	}
}
