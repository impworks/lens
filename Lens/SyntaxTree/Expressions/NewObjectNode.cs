using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new object creation.
	/// </summary>
	internal class NewObjectNode : InvocationNodeBase
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

		public override IEnumerable<NodeChild> GetChildren()
		{
			return Arguments.Select((expr, i) => new NodeChild(expr, x => Arguments[i] = x));
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var type = Resolve(ctx);
			if(type.IsVoid())
				error(CompilerMessages.VoidTypeDefault);

			if(type.IsAbstract)
				error(CompilerMessages.TypeAbstract, TypeSignature.FullSignature);

			if(Arguments.Count == 0)
				error(CompilerMessages.ParameterlessConstructorParens);

			var isParameterless = Arguments.Count == 1 && Arguments[0].Resolve(ctx) == typeof (Unit);

			var argTypes = isParameterless
				? Type.EmptyTypes
				: Arguments.Select(a => a.Resolve(ctx)).ToArray();

			try
			{
				var ctor = ctx.ResolveConstructor(type, argTypes);

				if (!isParameterless)
				{
					var destTypes = ctor.ArgumentTypes;
					for (var idx = 0; idx < Arguments.Count; idx++)
						Expr.Cast(Arguments[idx], destTypes[idx]).Emit(ctx, true);
				}

				gen.EmitCreateObject(ctor.ConstructorInfo);
			}
			catch (AmbiguousMatchException)
			{
				error(CompilerMessages.TypeConstructorAmbiguos, TypeSignature.FullSignature);
			}
			catch (KeyNotFoundException)
			{
				if (!isParameterless || !type.IsValueType)
					error(CompilerMessages.TypeConstructorNotFound, TypeSignature.FullSignature);

				var castExpr = Expr.Default(TypeSignature);
				castExpr.Emit(ctx, true);
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
			return string.Format("new({0}, args: {1})", TypeSignature.FullSignature, string.Join(";", Arguments));
		}
	}
}
