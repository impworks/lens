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
			Type = type;
		}
		
		/// <summary>
		/// The type of the object to create.
		/// </summary>
		public TypeSignature Type { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return ctx.ResolveType(Type.Signature);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Arguments;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var type = ctx.ResolveType(Type);
			if(type.IsVoid())
				Error(CompilerMessages.VoidTypeDefault);

			if(type.IsAbstract)
				Error(CompilerMessages.TypeAbstract, Type.Signature);

			if(Arguments.Count == 0)
				Error(CompilerMessages.ParameterlessConstructorParens);

			var isParameterless = Arguments.Count == 1 && Arguments[0].GetExpressionType(ctx) == typeof (Unit);

			var argTypes = isParameterless
				? System.Type.EmptyTypes
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
				Error(CompilerMessages.TypeConstructorAmbiguos, Type.Signature);
			}
			catch (KeyNotFoundException)
			{
				if (!isParameterless || !type.IsValueType)
					Error(CompilerMessages.TypeConstructorNotFound, Type.Signature);

				var castExpr = Expr.Default(Type);
				castExpr.Compile(ctx, true);
			}
		}

		#region Equality members

		protected bool Equals(NewObjectNode other)
		{
			return base.Equals(other) && Equals(Type, other.Type);
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
				return (base.GetHashCode() * 397) ^ (Type != null ? Type.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("new({0}, args: {1})", Type.Signature, string.Join(";", Arguments));
		}
	}
}
