using System.Collections.Generic;
using Lens.Compiler;
using Lens.SyntaxTree.Expressions;
using Lens.Translations;

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

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Value;
		}

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			var type = Value != null
				? Value.GetExpressionType(ctx)
				: ctx.ResolveType(Type);
			ctx.CheckTypedExpression(Value, type);

			if(LocalName != null)
				return;

			if(Name == "_")
				Error(CompilerMessages.UnderscoreName);

			try
			{
				var name = ctx.CurrentScope.DeclareName(Name, type, IsImmutable);
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

		protected override void compile(Context ctx, bool mustReturn)
		{
			var name = LocalName ?? ctx.CurrentScope.FindName(Name);
			if (name.IsConstant && name.IsImmutable && ctx.Options.UnrollConstants)
				return;

			if (Value == null)
				Value = Expr.Default(Type);

			var assignNode = new SetIdentifierNode
			{
				Identifier = Name,
				LocalName = LocalName,
				Value = Value,
				IsInitialization = true,
			};

			assignNode.Compile(ctx, mustReturn);
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
