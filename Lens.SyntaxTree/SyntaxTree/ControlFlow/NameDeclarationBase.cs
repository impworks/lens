using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base class for variable and constant declarations.
	/// </summary>
	public abstract class NameDeclarationBase : NodeBase, IStartLocationTrackingEntity
	{
		protected NameDeclarationBase(bool isConst)
		{
			IsConstant = isConst;
		}

		/// <summary>
		/// The name of the variable.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The value to assign to the variable.
		/// </summary>
		public NodeBase Value { get; set; }

		/// <summary>
		/// A flag indicating that the current value is contant.
		/// </summary>
		public readonly bool IsConstant;

		public override LexemLocation EndLocation
		{
			get { return Value.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Value;
		}

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			ctx.CurrentScope.DeclareName(Name, Value.GetExpressionType(ctx), IsConstant);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(NameDeclarationBase other)
		{
			return IsConstant.Equals(other.IsConstant) && string.Equals(Name, other.Name) && Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NameDeclarationBase) obj);
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
			return string.Format("{0}({1} = {2})", IsConstant ? "let" : "var", Name, Value);
		}
	}
}
