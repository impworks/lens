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
			var nameInfo = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if(nameInfo == null)
				Error("Variable '{0}' is undefined in current scope!", Identifier);

			if(nameInfo.IsConstant && !IsInitialization)
				Error("'{0}' is a constant and cannot be assigned after definition!", Identifier);

			var exprType = Value.GetExpressionType(ctx);
			if (!nameInfo.Type.IsExtendablyAssignableFrom(exprType))
				Error("Cannot assign a value of type '{0}' to a variable of type '{1}'!\nAn explicit cast might be required.", exprType, nameInfo.Type);

			if (nameInfo.IsClosured)
			{
				if (nameInfo.ClosureDistance == 0)
					assignClosuredLocal(ctx, nameInfo);
				else
					assignClosuredRemote(ctx, nameInfo);
			}
			else
				assignLocal(ctx, nameInfo);
		}

		private void assignLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			var castNode = Expr.Cast(Value, name.Type);

			if (!name.IsRefArgument)
			{
				castNode.Compile(ctx, true);
				gen.EmitSaveLocal(name);
			}
			else
			{
				var argId = ctx.CurrentMethod.Arguments.IndexOf(name.Name);
				if (!ctx.CurrentMethod.IsStatic)
					argId++;

				gen.EmitLoadArgument(argId, true);
				castNode.Compile(ctx, true);
				gen.EmitSaveObject(name.Type);
			}
		}

		/// <summary>
		/// Assigns a closured variable that is declared in current scope.
		/// </summary>
		private void assignClosuredLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadLocal(ctx.CurrentScope.ClosureVariable);
			
			Expr.Cast(Value, name.Type).Compile(ctx, true);

			var clsField = ctx.CurrentScope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitSaveField(clsField);
		}

		/// <summary>
		/// Assigns a closured variable that has been imported from outer scopes.
		/// </summary>
		private void assignClosuredRemote(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadArgument(0);

			var dist = name.ClosureDistance;
			var type = (Type)ctx.CurrentType.TypeBuilder;
			while (dist > 1)
			{
				var rootField = ctx.ResolveField(type, Scope.ParentScopeFieldName);
				gen.EmitLoadField(rootField);

				type = rootField.FieldType;
				dist--;
			}

			Expr.Cast(Value, name.Type).Compile(ctx, true);

			var clsField = ctx.ResolveField(type, name.ClosureFieldName);
			gen.EmitSaveField(clsField);
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
