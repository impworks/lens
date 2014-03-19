using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.SyntaxTree.Expressions;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base class for variable and constant declarations.
	/// </summary>
	internal abstract class NameDeclarationNodeBase : NodeBase
	{
		protected NameDeclarationNodeBase(string name, bool immutable)
		{
			Name = name;
			IsImmutable = immutable;
		}

		/// <summary>
		/// The name of the variable.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Explicitly specified local variable.
		/// </summary>
		public LocalName LocalName { get; set; }

		/// <summary>
		/// Type signature for non-initialized variables.
		/// </summary>
		public TypeSignature Type { get; set; }

		/// <summary>
		/// The value to assign to the variable.
		/// </summary>
		public NodeBase Value { get; set; }

		/// <summary>
		/// A flag indicating that the current value is read-only.
		/// </summary>
		public readonly bool IsImmutable;

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Value, x => Value = x);
		}

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var type = Value != null
				? Value.Resolve(ctx)
				: ctx.ResolveType(Type);

			ctx.CheckTypedExpression(Value, type);

			if (LocalName == null)
			{
				if (Name == "_")
					error(CompilerMessages.UnderscoreName);

				try
				{
					var name = ctx.CurrentScopeFrame.DeclareName(Name, type, IsImmutable);
					if (Value != null && Value.IsConstant && ctx.Options.UnrollConstants)
					{
						name.IsConstant = true;
						name.ConstantValue = Value.ConstantValue;
					}
				}
				catch (LensCompilerException ex)
				{
					ex.BindToLocation(this);
					throw;
				}
			}

			return base.resolve(ctx, mustReturn);
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			var name = LocalName ?? ctx.CurrentScopeFrame.FindName(Name);
			if (name.IsConstant && name.IsImmutable && ctx.Options.UnrollConstants)
				return Expr.Nop();

			return new SetIdentifierNode
			{
				Identifier = Name,
				LocalName = LocalName,
				Value = Value ?? Expr.Default(Type),
				IsInitialization = true,
			};
		}

		#region Equality members

		protected bool Equals(NameDeclarationNodeBase other)
		{
			return IsConstant.Equals(other.IsConstant) && string.Equals(Name, other.Name) && Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NameDeclarationNodeBase) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = IsConstant.GetHashCode();
				hashCode = (hashCode*397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode*397) ^ (Value != null ? Value.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("{0}({1} = {2})", IsImmutable ? "let" : "var", Name, Value);
		}
	}
}
