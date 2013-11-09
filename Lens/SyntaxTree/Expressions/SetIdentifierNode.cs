using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	internal class SetIdentifierNode : IdentifierNodeBase
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

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Value;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var exprType = Value.GetExpressionType(ctx);
			ctx.CheckTypedExpression(Value, exprType, true);

			var nameInfo = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (nameInfo != null)
			{
				if (nameInfo.IsImmutable && !IsInitialization)
					Error(CompilerMessages.IdentifierIsConstant, Identifier);

				if (!nameInfo.Type.IsExtendablyAssignableFrom(exprType))
					Error(CompilerMessages.IdentifierTypeMismatch, exprType, nameInfo.Type);

				if (nameInfo.IsClosured)
				{
					if (nameInfo.ClosureDistance == 0)
						assignClosuredLocal(ctx, nameInfo);
					else
						assignClosuredRemote(ctx, nameInfo);
				}
				else
				{
					assignLocal(ctx, nameInfo);
				}

				return;
			}

			try
			{
				var pty = ctx.ResolveGlobalProperty(Identifier);

				if(!pty.HasSetter)
					Error(CompilerMessages.GlobalPropertyNoSetter, Identifier);

				var type = pty.PropertyType;
				if(!type.IsExtendablyAssignableFrom(exprType))
					Error(CompilerMessages.GlobalPropertyTypeMismatch, exprType, type);

				var cast = Expr.Cast(Value, type);
				if (pty.SetterMethod != null)
				{
					cast.Compile(ctx, true);
					gen.EmitCall(pty.SetterMethod.MethodInfo);
				}
				else
				{
					var method = typeof (GlobalPropertyHelper).GetMethod("Set").MakeGenericMethod(type);

					gen.EmitConstant(ctx.ContextId);
					gen.EmitConstant(pty.PropertyId);
					Expr.Cast(Value, type).Compile(ctx, true);
					gen.EmitCall(method);
				}
			}
			catch (KeyNotFoundException)
			{
				Error(CompilerMessages.VariableNotFound, Identifier);
			}
		}

		private void assignLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			var castNode = Expr.Cast(Value, name.Type);

			if (!name.IsRefArgument)
			{
				castNode.Compile(ctx, true);
				if(name.ArgumentId.HasValue)
					gen.EmitSaveArgument(name.ArgumentId.Value);
				else
					gen.EmitSaveLocal(name);
			}
			else
			{
				gen.EmitLoadArgument(name.ArgumentId.Value);
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

			var clsType = ctx.CurrentScope.ClosureType.TypeInfo;
			var clsField = ctx.ResolveField(clsType, name.ClosureFieldName);
			gen.EmitSaveField(clsField.FieldInfo);
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
				var rootField = ctx.ResolveField(type, EntityNames.ParentScopeFieldName);
				gen.EmitLoadField(rootField.FieldInfo);

				type = rootField.FieldType;
				dist--;
			}

			Expr.Cast(Value, name.Type).Compile(ctx, true);

			var clsField = ctx.ResolveField(type, name.ClosureFieldName);
			gen.EmitSaveField(clsField.FieldInfo);
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
