using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a member of a type, either field or property.
	/// </summary>
	internal class GetMemberNode : MemberNodeBase, IPointerProvider
	{
		public GetMemberNode()
		{
			TypeHints = new List<TypeSignature>();
		}

		private bool m_IsResolved;

		private Type m_Type;
		private FieldWrapper m_Field;
		private MethodWrapper m_Method;
		private PropertyWrapper m_Property;

		private bool m_IsStatic;

		public bool PointerRequired { get; set; }
		public bool RefArgumentRequired { get; set; }

		/// <summary>
		/// The list of type signatures if the given identifier is a method.
		/// </summary>
		public List<TypeSignature> TypeHints { get; set; }

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			if (!m_IsResolved)
				resolveSelf(ctx);

			if (m_Type != null)
				checkTypeInSafeMode(ctx, m_Type);

			if (Expression != null && Expression.Resolve(ctx).IsArray && MemberName == "Length")
				return typeof (int);

			if (m_Field != null)
				return m_Field.FieldType;

			if (m_Property != null)
				return m_Property.PropertyType;

			return m_Method.ReturnType == typeof (void)
				? FunctionalHelper.CreateActionType(m_Method.ArgumentTypes)
				: FunctionalHelper.CreateFuncType(m_Method.ReturnType, m_Method.ArgumentTypes);
		}

		private void resolveSelf(Context ctx)
		{
			Action check = () =>
			{
				if (Expression == null && !m_IsStatic)
					error(CompilerMessages.DynamicMemberFromStaticContext, MemberName);

				if(m_Method == null && TypeHints.Count > 0)
					error(CompilerMessages.TypeArgumentsForNonMethod, m_Type, MemberName);

				m_IsResolved = true;
			};

			m_Type = StaticType != null
				? ctx.ResolveType(StaticType)
				: Expression.Resolve(ctx);

			// special case: array length
			if (m_Type.IsArray && MemberName == "Length")
			{
				check();
				return;
			}

			// check for field
			try
			{
				m_Field = ctx.ResolveField(m_Type, MemberName);
				m_IsStatic = m_Field.IsStatic;

				check();
				return;
			}
			catch (KeyNotFoundException) { }

			// check for property
			try
			{
				m_Property = ctx.ResolveProperty(m_Type, MemberName);

				if(!m_Property.CanGet)
					error(CompilerMessages.PropertyNoGetter, m_Type, MemberName);

				m_IsStatic = m_Property.IsStatic;

				check();
				return;
			}
			catch (KeyNotFoundException)
			{ }

			var argTypes = TypeHints.Select(t => t.FullSignature == "_" ? null : ctx.ResolveType(t)).ToArray();
			var methods = ctx.ResolveMethodGroup(m_Type, MemberName).Where(m => checkMethodArgs(ctx, argTypes, m)).ToArray();

			if (methods.Length == 0)
				error(argTypes.Length == 0 ? CompilerMessages.TypeIdentifierNotFound : CompilerMessages.TypeMethodNotFound, m_Type.Name, MemberName);

			if (methods.Length > 1)
				error(CompilerMessages.TypeMethodAmbiguous, m_Type.Name, MemberName);

			m_Method = methods[0];
			if (m_Method.ArgumentTypes.Length > 16)
				error(CompilerMessages.CallableTooManyArguments);

			m_IsStatic = m_Method.IsStatic;

			check();
		}

		private bool checkMethodArgs(Context ctx, Type[] argTypes, MethodWrapper method)
		{
			if(argTypes.Length == 0)
				return true;

			if (method.ArgumentTypes.Length != argTypes.Length)
				return false;

			return !method.ArgumentTypes.Where((p, idx) => argTypes[idx] != null && p != argTypes[idx]).Any();
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			if(!m_IsResolved)
				resolveSelf(ctx);

			var gen = ctx.CurrentILGenerator;
			
			if (!m_IsStatic)
			{
				var exprType = Expression.Resolve(ctx);
				if (exprType.IsStruct())
				{
					if (Expression is IPointerProvider)
					{
						(Expression as IPointerProvider).PointerRequired = true;
						Expression.Emit(ctx, true);
					}
					else
					{
						var tmpVar = ctx.CurrentScopeFrame.DeclareImplicitName(ctx, exprType, false);
						Expression.Emit(ctx, true);
						gen.EmitSaveLocal(tmpVar);
						gen.EmitLoadLocal(tmpVar, true);
					}
				}
				else
				{
					Expression.Emit(ctx, true);
				}

				if (exprType.IsArray && MemberName == "Length")
					gen.EmitGetArrayLength();
			}
	
			if (m_Field != null)
			{
				if (m_Field.IsLiteral)
				{
					var fieldType = m_Field.FieldType;
					var dataType = fieldType.IsEnum ? Enum.GetUnderlyingType(fieldType) : fieldType;

					var value = m_Field.FieldInfo.GetValue(null);

					if (dataType == typeof(int))
						gen.EmitConstant((int) value);
					else if (dataType == typeof(long))
						gen.EmitConstant((long)value);
					else if (dataType == typeof(double))
						gen.EmitConstant((double)value);
					else if (dataType == typeof(float))
						gen.EmitConstant((float)value);

					else if(dataType == typeof(uint))
						gen.EmitConstant(unchecked((int)(uint)value));
					else if (dataType == typeof(ulong))
						gen.EmitConstant(unchecked((long)(ulong)value));

					else if (dataType == typeof(byte))
						gen.EmitConstant((byte)value);
					else if (dataType == typeof(sbyte))
						gen.EmitConstant((sbyte)value);
					else if (dataType == typeof(short))
						gen.EmitConstant((short)value);
					else if (dataType == typeof(ushort))
						gen.EmitConstant((ushort)value);
					else if (dataType == typeof(string))
						gen.EmitConstant((string)value);
					else
						throw new NotImplementedException("Unknown literal field type!");
				}
				else
				{ 
					gen.EmitLoadField(m_Field.FieldInfo, PointerRequired || RefArgumentRequired);
				}
				return;
			}

			if (m_Property != null)
			{
				if (m_Property.PropertyType.IsValueType && RefArgumentRequired)
					error(CompilerMessages.PropertyValuetypeRef, m_Property.Type, MemberName, m_Property.PropertyType);

				gen.EmitCall(m_Property.Getter);

				if (PointerRequired)
				{
					var tmpVar = ctx.CurrentScopeFrame.DeclareImplicitName(ctx, m_Property.PropertyType, false);
					gen.EmitSaveLocal(tmpVar);
					gen.EmitLoadLocal(tmpVar, true);
				}

				return;
			}

			if (m_Method != null)
			{
				if(RefArgumentRequired)
					error(CompilerMessages.MethodRef);

				if (m_IsStatic)
					gen.EmitNull();

				var retType = m_Method.ReturnType;
				var type = retType.IsNotVoid()
					? FunctionalHelper.CreateFuncType(retType, m_Method.ArgumentTypes)
					: FunctionalHelper.CreateActionType(m_Method.ArgumentTypes);

				var ctor = type.GetConstructor(new[] { typeof(object), typeof(IntPtr) });
				gen.EmitLoadFunctionPointer(m_Method.MethodInfo);
				gen.EmitCreateObject(ctor);
			}
		}

		public override string ToString()
		{
			var typehints = TypeHints.Any() ? "<" + string.Join(", ", TypeHints) + ">" : string.Empty;
			return StaticType == null
				? string.Format("getmbr({0}{1} of value {2})", MemberName, typehints, Expression)
				: string.Format("getmbr({0}{1} of type {2})", MemberName, typehints, StaticType);
		}

		#region Equality

		protected bool Equals(GetMemberNode other)
		{
			return base.Equals(other)
				   && PointerRequired.Equals(other.PointerRequired)
				   && RefArgumentRequired.Equals(other.RefArgumentRequired)
				   && TypeHints.SequenceEqual(other.TypeHints);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((GetMemberNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ PointerRequired.GetHashCode();
				hashCode = (hashCode * 397) ^ RefArgumentRequired.GetHashCode();
				hashCode = (hashCode * 397) ^ (TypeHints != null ? TypeHints.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
