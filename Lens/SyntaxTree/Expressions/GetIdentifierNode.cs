﻿using System;
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
	internal class GetIdentifierNode : IdentifierNodeBase, IPointerProvider
	{
		private MethodEntity _Method;
		private GlobalPropertyInfo _Property;
		private LocalName _LocalConstant;
		private TypeEntity _Type;

		public bool PointerRequired { get; set; }
		public bool RefArgumentRequired { get; set; }

		public override bool IsConstant { get { return _LocalConstant != null; } }
		public override dynamic ConstantValue { get { return _LocalConstant != null ? _LocalConstant.ConstantValue : base.ConstantValue; } }

		public GetIdentifierNode(string identifier = null)
		{
			Identifier = identifier;
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (_Type != null)
				return Expr.New(_Type.TypeInfo);

			return base.Expand(ctx, mustReturn);
		}

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			// local variable
			var local = LocalName ?? ctx.Scope.FindName(Identifier);
			if (local != null)
			{
				// only local constants are cached
				// because mutable variables could be closured later on
				if (local.IsConstant && local.IsImmutable && ctx.Options.UnrollConstants)
					_LocalConstant = local;

				return local.Type;
			}

			// static function declared in the script
			try
			{
				var methods = ctx.MainType.ResolveMethodGroup(Identifier);
				if (methods.Length > 1)
					error(CompilerMessages.FunctionInvocationAmbiguous, Identifier);

				_Method = methods[0];
				return FunctionalHelper.CreateFuncType(_Method.ReturnType, _Method.GetArgumentTypes(ctx));
			}
			catch (KeyNotFoundException) { }

			// algebraic type without a constructor
			var type = ctx.FindType(Identifier);
			if (type != null && type.Kind == TypeEntityKind.TypeLabel)
			{
				try
				{
					type.ResolveConstructor(new Type[0]);
					_Type = type;
					return _Type.TypeInfo;
				}
				catch (KeyNotFoundException) { }
			}

			// global property
			try
			{
				_Property = ctx.ResolveGlobalProperty(Identifier);
				return _Property.PropertyType;
			}
			catch (KeyNotFoundException)
			{
				error(CompilerMessages.IdentifierNotFound, Identifier);
			}

			return typeof (Unit);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var resultType = Resolve(ctx);

			var gen = ctx.CurrentMethod.Generator;

			// local name is not cached because it can be closured.
			// if the identifier is actually a local constant, the 'compile' method is not invoked at all
			var local = LocalName ?? ctx.Scope.FindName(Identifier);
			if (local != null)
			{
				if(local.IsImmutable && RefArgumentRequired)
					error(CompilerMessages.ConstantByRef);

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
			if (_Method != null)
			{
				var ctor = resultType.GetConstructor(new[] {typeof (object), typeof (IntPtr)});

				gen.EmitNull();
				gen.EmitLoadFunctionPointer(_Method.MethodInfo);
				gen.EmitCreateObject(ctor);

				return;
			}

			// get a property value
			if (_Property != null)
			{
				var id = _Property.PropertyId;
				if(!_Property.HasGetter)
					error(CompilerMessages.GlobalPropertyNoGetter, Identifier);

				var type = _Property.PropertyType;
				if (_Property.GetterMethod != null)
				{
					gen.EmitCall(_Property.GetterMethod.MethodInfo);
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

			error(CompilerMessages.IdentifierNotFound, Identifier);
		}

		/// <summary>
		/// Gets a closured variable that has been declared in the current scope.
		/// </summary>
		private void getClosuredLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentMethod.Generator;

			gen.EmitLoadLocal(ctx.Scope.ClosureVariable);

			var clsField = ctx.Scope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitLoadField(clsField.FieldBuilder, PointerRequired || RefArgumentRequired);
		}

		/// <summary>
		/// Gets a closured variable that has been imported from outer scopes.
		/// </summary>
		private void getClosuredRemote(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentMethod.Generator;

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

			var clsField = ctx.ResolveField(type, name.ClosureFieldName);
			gen.EmitLoadField(clsField.FieldInfo, PointerRequired || RefArgumentRequired);
		}

		private void getLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentMethod.Generator;
			var ptr = PointerRequired || RefArgumentRequired;

			if (name.ArgumentId.HasValue)
			{
				gen.EmitLoadArgument(name.ArgumentId.Value, ptr);
				if(name.IsRefArgument && !ptr)
					gen.EmitLoadFromPointer(name.Type);
			}
			else
			{
				gen.EmitLoadLocal(name, ptr);
			}
		}

		public override string ToString()
		{
			return string.Format("get({0})", Identifier);
		}

		#region Equality

		protected bool Equals(GetIdentifierNode other)
		{
			return base.Equals(other)
				   && RefArgumentRequired.Equals(other.RefArgumentRequired)
				   && PointerRequired.Equals(other.PointerRequired);
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
				var hash = base.GetHashCode();
				hash = (hash * 397) ^ PointerRequired.GetHashCode();
				hash = (hash * 397) ^ RefArgumentRequired.GetHashCode();
				return hash;
			}
		}

		#endregion
	}
}
