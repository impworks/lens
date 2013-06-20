using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new object creation.
	/// </summary>
	public class NewObjectNode : InvocationNodeBase
	{
		public NewObjectNode(string type = null)
		{
			TypeSignature = type;
		}

		/// <summary>
		/// The type of the object to create.
		/// </summary>
		public Type Type;

		/// <summary>
		/// The type signature of the object to create.
		/// </summary>
		public TypeSignature TypeSignature;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return Type ?? ctx.ResolveType(TypeSignature);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Arguments;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var type = GetExpressionType(ctx);
			if(type.IsVoid())
				Error(CompilerMessages.VoidTypeDefault);

			if(type.IsAbstract)
				Error(CompilerMessages.TypeAbstract, TypeSignature.Signature);

			if(Arguments.Count == 0)
				Error(CompilerMessages.ParameterlessConstructorParens);

			var isParameterless = Arguments.Count == 1 && Arguments[0].GetExpressionType(ctx) == typeof (Unit);

			var argTypes = isParameterless
				? Type.EmptyTypes
				: Arguments.Select(a => a.GetExpressionType(ctx)).ToArray();

			try
			{
				var ctor = ctx.ResolveConstructor(type, argTypes);

				if (!isParameterless)
				{
					var destTypes = ctor.ArgumentTypes;
					for (var idx = 0; idx < Arguments.Count; idx++)
						Expr.Cast(Arguments[idx], destTypes[idx]).Compile(ctx, true);
				}

				gen.EmitCreateObject(ctor.ConstructorInfo);
			}
			catch (AmbiguousMatchException)
			{
				Error(CompilerMessages.TypeConstructorAmbiguos, TypeSignature.Signature);
			}
			catch (KeyNotFoundException)
			{
				if (!isParameterless || !type.IsValueType)
					Error(CompilerMessages.TypeConstructorNotFound, TypeSignature.Signature);

				var castExpr = Expr.Default(TypeSignature);
				castExpr.Compile(ctx, true);
			}
		}

		#region Equality members

		protected bool Equals(NewObjectNode other)
		{
			return base.Equals(other) && Equals(TypeSignature, other.TypeSignature);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NewObjectNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("new({0}, args: {1})", TypeSignature.Signature, string.Join(";", Arguments));
		}
	}
}
