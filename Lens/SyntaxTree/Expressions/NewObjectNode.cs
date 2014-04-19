using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Operators;
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

		/// <summary>
		/// Checks if constructor call may be replaced with 'default' initialization.
		/// </summary>
		private bool _IsDefault;

		private ConstructorWrapper _Constructor;
		protected override CallableWrapperBase _Wrapper { get { return _Constructor; } }

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			base.resolve(ctx, true);

			var type = Type ?? ctx.ResolveType(TypeSignature);

			if (type.IsVoid())
				error(CompilerMessages.VoidTypeDefault);

			if (type.IsAbstract)
				error(CompilerMessages.TypeAbstract, TypeSignature.FullSignature);

			if (type.IsInterface)
				error(CompilerMessages.TypeInterface, TypeSignature.FullSignature);

			if (Arguments.Count == 0)
				error(CompilerMessages.ParameterlessConstructorParens);

			try
			{
				_Constructor = ctx.ResolveConstructor(type, _ArgTypes);
			}
			catch (AmbiguousMatchException)
			{
				error(CompilerMessages.TypeConstructorAmbiguos, TypeSignature.FullSignature);
			}
			catch (KeyNotFoundException)
			{
				if (_ArgTypes.Length > 0 || !type.IsValueType)
					error(CompilerMessages.TypeConstructorNotFound, TypeSignature.FullSignature);

				_IsDefault = true;
				return type;
			}

			applyLambdaArgTypes(ctx);

			return resolvePartial(_Constructor, type, _ArgTypes);
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (_IsDefault)
				return new DefaultOperatorNode {Type = Type, TypeSignature = TypeSignature};

			return base.Expand(ctx, mustReturn);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if(_Constructor != null)
			{
				if (_ArgTypes.Length > 0)
				{
					var destTypes = _Constructor.ArgumentTypes;
					for (var idx = 0; idx < Arguments.Count; idx++)
						Expr.Cast(Arguments[idx], destTypes[idx]).Emit(ctx, true);
				}

				gen.EmitCreateObject(_Constructor.ConstructorInfo);
			}
			else
			{
				Expr.Default(TypeSignature).Emit(ctx, true);
			}
		}

		protected override InvocationNodeBase recreateSelfWithArgs(IEnumerable<NodeBase> newArgs)
		{
			return new NewObjectNode {Type = Type, TypeSignature = TypeSignature, Arguments = newArgs.ToList() };
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
