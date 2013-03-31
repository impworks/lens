using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	public class GetIdentifierNode : IdentifierNodeBase, IEndLocationTrackingEntity, IPointerProvider
	{
		private MethodEntity m_Method;
		private GlobalPropertyInfo m_Property;

		/// <summary>
		/// Local and closured variables can provide a location pointer.
		/// </summary>
		public bool PointerRequired { get; set; }

		public GetIdentifierNode(string identifier = null)
		{
			Identifier = identifier;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var local = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (local != null)
				return local.Type;

			try
			{
				m_Method = ctx.MainType.ResolveMethod(Identifier, Type.EmptyTypes);
				return FunctionalHelper.CreateFuncType(m_Method.ReturnType);
			}
			catch (KeyNotFoundException) { }

			try
			{
				m_Property = ctx.ResolveGlobalProperty(Identifier);
				return m_Property.PropertyType;
			}
			catch (KeyNotFoundException)
			{
				Error("No local variable or global parameterless function named '{0}' was found.", Identifier);
			}

			return typeof (Unit);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var resultType = GetExpressionType(ctx);

			var gen = ctx.CurrentILGenerator;

			// load local variable
			// local name is not cached because it can be closured.
			var local = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (local != null)
			{
				if(local.IsConstant && PointerRequired)
					Error("Constant variables cannot be passed by reference!");

				if (local.IsClosured)
				{
					if (local.ClosureDistance == 0)
						getClosuredLocal(ctx, local);
					else
						getClosuredRemote(ctx, local);
				}
				else
				{
					getLocal(ctx, local);
				}

				return;
			}

			// load pointer to global function
			if (m_Method != null)
			{
				var ctor = resultType.GetConstructor(new[] {typeof (object), typeof (IntPtr)});

				gen.EmitNull();
				gen.EmitLoadFunctionPointer(m_Method.MethodInfo);
				gen.EmitCreateObject(ctor);

				return;
			}

			// get a property value
			if (m_Property != null)
			{
				var id = m_Property.PropertyId;
				if(!m_Property.HasGetter)
					Error("Global property '{0}' has no getter!", Identifier);

				var type = m_Property.PropertyType;
				if (m_Property.GetterMethod != null)
				{
					gen.EmitCall(m_Property.GetterMethod.MethodInfo);
				}
				else
				{
					var method = typeof (GlobalPropertyHelper).GetMethod("Get").MakeGenericMethod(type);
					gen.EmitConstant(ctx.ContextId);
					gen.EmitConstant(id);
					gen.EmitCall(method);
				}
				return;
			}

			Error("No local variable or global parameterless function named '{0}' was found.", Identifier);
		}

		/// <summary>
		/// Gets a closured variable that has been declared in the current scope.
		/// </summary>
		private void getClosuredLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadLocal(ctx.CurrentScope.ClosureVariable);

			var clsField = ctx.CurrentScope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitLoadField(clsField.FieldBuilder, PointerRequired);
		}

		/// <summary>
		/// Gets a closured variable that has been imported from outer scopes.
		/// </summary>
		private void getClosuredRemote(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadArgument(0);

			var dist = name.ClosureDistance;
			var type = (Type)ctx.CurrentType.TypeBuilder;
			while (dist > 1)
			{
				var rootField = ctx.ResolveField(type, Scope.ParentScopeFieldName);
				gen.EmitLoadField(rootField.FieldInfo);

				type = rootField.FieldType;
				dist--;
			}

			var clsField = ctx.ResolveField(type, name.ClosureFieldName);
			gen.EmitLoadField(clsField.FieldInfo, PointerRequired);
		}

		private void getLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadLocal(name, PointerRequired);
		}

		public override string ToString()
		{
			return string.Format("get({0})", Identifier);
		}

		#region Equality

		protected bool Equals(GetIdentifierNode other)
		{
			return base.Equals(other) && PointerRequired.Equals(other.PointerRequired);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((GetIdentifierNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ PointerRequired.GetHashCode();
			}
		}

		#endregion
	}
}
