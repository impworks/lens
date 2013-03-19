using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	public class GetIdentifierNode : IdentifierNodeBase, IEndLocationTrackingEntity, IPointerProvider
	{
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
			var nameInfo = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (nameInfo != null)
				return nameInfo.Type;

			try
			{
				var method = ctx.MainType.ResolveMethod(Identifier, Type.EmptyTypes);
				if(method == null)
					throw new KeyNotFoundException();

				return FunctionalHelper.CreateFuncType(method.ReturnType);
			}
			catch (KeyNotFoundException)
			{
				Error("No local variable or global parameterless function named '{0}' was found.", Identifier);
			}

			return typeof (Unit);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			// load local variable
			var nameInfo = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (nameInfo != null)
			{
				if(nameInfo.IsConstant && PointerRequired)
					Error("Constant variables cannot be passed by reference!");

				if (nameInfo.IsClosured)
				{
					if(nameInfo.ClosureDistance == 0)
						getClosuredLocal(ctx, nameInfo);
					else
						getClosuredRemote(ctx, nameInfo);
				}
				else
				{
					getLocal(ctx, nameInfo);
				}

				return;
			}

			// load pointer to global function
			try
			{
				var method = ctx.MainType.ResolveMethod(Identifier, Type.EmptyTypes);
				var funcType = FunctionalHelper.CreateFuncType(method.ReturnType);
				var ctor = funcType.GetConstructor(new[] {typeof (object), typeof (IntPtr)});

				gen.EmitNull();
				gen.EmitLoadFunctionPointer(method);
				gen.EmitCreateObject(ctor);
			}
			catch (KeyNotFoundException)
			{
				Error("No local variable or global parameterless function named '{0}' was found.", Identifier);
			}
		}

		/// <summary>
		/// Gets a closured variable that has been declared in the current scope.
		/// </summary>
		private void getClosuredLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;

			gen.EmitLoadLocal(ctx.CurrentScope.ClosureVariable);

			var clsField = ctx.CurrentScope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitLoadField(clsField, PointerRequired);
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
				gen.EmitLoadField(rootField);

				type = rootField.FieldType;
				dist--;
			}

			var clsField = ctx.ResolveField(type, name.ClosureFieldName);
			gen.EmitLoadField(clsField, PointerRequired);
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
