using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Translations;

namespace Lens.Compiler.Entities
{
	using System;


	internal class GenericParameterEntity
	{
		public GenericParameterEntity (MethodEntity method, GenericTypeParameterBuilder builder, string name, bool isNew, bool isRef, bool isVal, TypeSignature[] baseTypes)
		{
			ContainerMethod = method;
			ParamBuilder = builder;

			Name = name;
			HasNewConstraint = isNew;
			HasRefConstraint = isRef;
			HasValConstraint = isVal;
			TypeConstraints = baseTypes;
		}

		#region Properties

		public readonly MethodEntity ContainerMethod;

		/// <summary>
		/// Generic parameter name.
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// The generic parameter builder for current entity.
		/// </summary>
		public readonly GenericTypeParameterBuilder ParamBuilder;

		/// <summary>
		/// Flag indicating that the type must have a parameterless constructor.
		/// </summary>
		public readonly bool HasNewConstraint;

		/// <summary>
		/// Flag indicating that the type must be passed by reference (class).
		/// </summary>
		public readonly bool HasRefConstraint;

		/// <summary>
		/// Flag indicating that the type must be passed by value (struct).
		/// </summary>
		public readonly bool HasValConstraint;

		/// <summary>
		/// List of interface and base type signatures.
		/// </summary>
		public readonly TypeSignature[] TypeConstraints;

		#endregion

		#region Methods

		public void PrepareSelf()
		{
			var ctx = ContainerMethod.ContainerType.Context;

			// where T: ref, val
			// type cannot be by-ref and by-val at the same time
			if (HasValConstraint && HasRefConstraint)
				Context.Error(CompilerMessages.GenericParameterRefVal, Name);

			// where T : A, B, A
			// base type has been specified more than once
			var duplicatedType = TypeConstraints.GroupBy(x => x.FullSignature).FirstOrDefault(x => x.Count() > 1);
			if(duplicatedType != null)
				Context.Error(CompilerMessages.GenericParameterBaseDuplicate, Name, duplicatedType.Key);

			var typeConstraints = TypeConstraints.Select(x => ctx.ResolveType(x)).ToArray();

			// no restrictions on types rejected in C#: System.Array, System.Delegate, System.Enum
			// System.Object and System.ValueType have "ref" and "val" keys instead
			var restricted = new[]
			{
				new { Type = typeof (object), ConstraintKey = "ref" },
				new { Type = typeof (ValueType), ConstraintKey = "val" },
			};

			foreach (var curr in restricted)
			{
				if (typeConstraints.Contains(curr.Type))
					Context.Error(CompilerMessages.GenericParameterBaseRestrictedType, Name, curr.Type.Name, curr.ConstraintKey);
			}

			// where T1: T2
			// generic argument cannot have other generic arguments as base (unlike in C#)
			var genericsNames = string.Join(", ", typeConstraints.Where(x => x is GenericTypeParameterBuilder).Select(x => '"' + x.Name + '"'));
			if (!string.IsNullOrEmpty(genericsNames))
				Context.Error(CompilerMessages.GenericParameterGenericBased, Name, genericsNames);

			// where T1: 
			// type cannot have more than one base type
			var baseNames = typeConstraints.Where(x => !x.IsInterface).ToList();
			if (baseNames.Count > 1)
				Context.Error(CompilerMessages.GenericParameterManyBaseTypes, Name, string.Join(", ", baseNames.Select(x => '"' + x.Name + '"')));

			var attrs = GenericParameterAttributes.None;

			if(HasNewConstraint) attrs |= GenericParameterAttributes.DefaultConstructorConstraint;
			if(HasRefConstraint) attrs |= GenericParameterAttributes.ReferenceTypeConstraint;
			if(HasValConstraint) attrs |= GenericParameterAttributes.NotNullableValueTypeConstraint;

			ParamBuilder.SetGenericParameterAttributes(attrs);
			ParamBuilder.SetBaseTypeConstraint(baseNames[0]);
			ParamBuilder.SetInterfaceConstraints(typeConstraints.Where(x => x.IsInterface).ToArray());
		}

		#endregion
	}
}
