using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	public class SetIdentifierNode : IdentifierNodeBase
	{
		public SetIdentifierNode(string identifier = null)
		{
			Identifier = identifier;
		}

		/// <summary>
		/// A flag indicating that assignment to a constant variable is legal
		/// because it's being instantiated.
		/// </summary>
		public bool IsInitialization { get; set; }

		/// <summary>
		/// Value to be assigned.
		/// </summary>
		public NodeBase Value { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Value.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Value;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var nameInfo = ctx.CurrentScope.FindName(Identifier);
			if(nameInfo == null)
				Error("Variable '{0}' is undefined in current scope!", Identifier);

			if(nameInfo.IsConstant && !IsInitialization)
				Error("'{0}' is a constant and cannot be assigned after definition!", Identifier);

			var exprType = Value.GetExpressionType(ctx);
			if (!nameInfo.Type.IsExtendablyAssignableFrom(exprType))
				Error("Cannot assign a value of type '{0}' to a variable of type '{1}'! An explicit cast might be required.", exprType, nameInfo.Type);

			if(nameInfo.IsClosured)
				assignClosured(ctx, nameInfo);
			else
				assignLocal(ctx, nameInfo);
		}

		private void assignLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			Value.Compile(ctx, true);
			convertIfNeeded(ctx, name);

			gen.EmitSaveLocal(name.LocalId.Value);
		}

		private void assignClosured(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitLoadLocal(ctx.CurrentScope.ClosureVariableId.Value);

			var dist = name.ClosureDistance;
			var scope = ctx.CurrentScope;
			while (dist > 0)
			{
				var rootField = scope.ClosureType.ResolveField(Scope.ParentScopeFieldName);
				gen.EmitLoadField(rootField);

				scope = scope.OuterScope;
				dist--;
			}

			Value.Compile(ctx, true);
			convertIfNeeded(ctx, name);

			var clsField = scope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitSaveField(clsField);
		}

		/// <summary>
		/// Boxes value types to object and upcasts numeric types if required.
		/// </summary>
		private void convertIfNeeded(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			var varType = name.Type;
			var exprType = Value.GetExpressionType(ctx);

			if(varType == typeof(object) && exprType.IsValueType)
				gen.EmitBox(exprType);

			if (varType != exprType && varType.IsNumericType() && exprType.IsNumericType())
				gen.EmitConvert(varType);
		}

		#region Equality members

		protected bool Equals(SetIdentifierNode other)
		{
			return base.Equals(other) && Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((SetIdentifierNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("set({0} = {1})", Identifier, Value);
		}
	}
}
