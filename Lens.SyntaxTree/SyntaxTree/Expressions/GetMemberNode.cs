using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a member of a type, either field or property.
	/// </summary>
	public class GetMemberNode : MemberNodeBase, IEndLocationTrackingEntity, IPointerProvider
	{
		private bool m_IsResolved;

		private PropertyInfo m_Property;
		private FieldInfo m_Field;
		private MethodInfo m_Method;

		private bool m_IsStatic;

		/// <summary>
		/// If the member is a field, its pointer can be returned.
		/// </summary>
		public bool PointerRequired { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!m_IsResolved)
				resolve(ctx);

			if (Expression != null && Expression.GetExpressionType(ctx).IsArray && MemberName == "Length")
				return typeof (int);

			if (m_Field != null)
				return m_Field.FieldType;

			if (m_Property != null)
				return m_Property.PropertyType;

			var argTypes = m_Method.GetParameters().Select(p => p.ParameterType).ToArray();
			return m_Method.ReturnType == typeof (void)
				? FunctionalHelper.CreateActionType(argTypes)
				: FunctionalHelper.CreateFuncType(m_Method.ReturnType, argTypes);
		}

		private void resolve(Context ctx)
		{
			Action checkStatic = () =>
			{
				if (Expression == null && !m_IsStatic)
					Error("'{0}' cannot be accessed from static context!");

				m_IsResolved = true;
			};

			var type = StaticType != null
				? ctx.ResolveType(StaticType)
				: Expression.GetExpressionType(ctx);

			// special case: array length
			if (type.IsArray && MemberName == "Length")
			{
				checkStatic();
				return;
			}

			// check for field
			try
			{
				m_Field = ctx.ResolveField(type, MemberName);
				m_IsStatic = m_Field.IsStatic;

				checkStatic();
				return;
			}
			catch (KeyNotFoundException) { }

			// check for property
			try
			{
				m_Property = ctx.ResolveProperty(type, MemberName);
				if (!m_Property.CanRead)
					Error("Property '{0}' of type '{1}' does not have a getter!", m_Property.Name, type);

				m_IsStatic = m_Property.GetGetMethod().IsStatic;

				checkStatic();
				return;
			}
			catch (KeyNotFoundException)
			{ }

			try
			{
				var methods = ctx.ResolveMethodGroup(type, MemberName);
				if (methods.Length > 1)
					Error("Type '{0}' has more than one suitable override of '{1}'!", type.Name, MemberName);

				m_Method = methods[0];
				if (m_Method.GetParameters().Count() > 16)
					Error("Cannot create a callable object from a method with more than 16 arguments!");

				m_IsStatic = m_Method.IsStatic;

				checkStatic();
			}
			catch (KeyNotFoundException)
			{
				Error("Type '{0}' does not have any field, property or method called '{1}'!", type.Name, MemberName);
			}
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			if(!m_IsResolved)
				resolve(ctx);

			var gen = ctx.CurrentILGenerator;
			
			if (!m_IsStatic)
			{
				var exprType = Expression.GetExpressionType(ctx);
				if (Expression is IPointerProvider)
					(Expression as IPointerProvider).PointerRequired = exprType.IsValueType && !exprType.IsNumericType();

				Expression.Compile(ctx, true);

				if (exprType.IsArray && MemberName == "Length")
					gen.EmitGetArrayLength();
			}
	
			if (m_Field != null)
			{
				gen.EmitLoadField(m_Field, PointerRequired);
				return;
			}

			if (m_Property != null)
			{
				var getter = m_Property.GetGetMethod();
				gen.EmitCall(getter);
				return;
			}

			if (m_Method != null)
			{
				if (m_IsStatic)
					gen.EmitNull();

				var retType = m_Method.ReturnType;
				var args = m_Method.GetParameters().Select(p => p.ParameterType).ToArray();
				var type = retType.IsNotVoid()
					? FunctionalHelper.CreateFuncType(retType, args)
					: FunctionalHelper.CreateActionType(args);

				var ctor = type.GetConstructor(new[] { typeof(object), typeof(IntPtr) });
				gen.EmitLoadFunctionPointer(m_Method);
				gen.EmitCreateObject(ctor);
			}
		}

		public override string ToString()
		{
			return StaticType == null
				? string.Format("getmbr({0} of value {1})", MemberName, Expression)
				: string.Format("getmbr({0} of type {1})", MemberName, StaticType);
		}
	}
}
