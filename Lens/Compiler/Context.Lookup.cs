using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;

namespace Lens.Compiler
{
	internal partial class Context
	{
		/// <summary>
		/// Finds a locally declared type.
		/// </summary>
		public TypeEntity FindType(string name)
		{
			TypeEntity declared;
			_DefinedTypes.TryGetValue(name, out declared);
			return declared;
		}

		/// <summary>
		/// Resolves a type by its string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			return ResolveType(TypeSignature.Parse(signature));
		}

		/// <summary>
		/// Resolves a type by its signature.
		/// </summary>
		public Type ResolveType(TypeSignature signature, bool allowUnspecified = false)
		{
			if (allowUnspecified && signature.FullSignature == "_")
				return null;

			var declared = FindType(signature.FullSignature);
			return declared != null
				? declared.TypeInfo
				: _TypeResolver.ResolveType(signature);
		}

		/// <summary>
		/// Resolves a field from a type by its name, including declared types.
		/// </summary>
		public FieldWrapper ResolveField(Type type, string name)
		{
			if (!(type is TypeBuilder))
				return ReflectionHelper.ResolveField(type, name);

			var typeEntity = _DefinedTypes[type.Name];
			var fi = typeEntity.ResolveField(name);
			return new FieldWrapper
			{
				Name = name,
				Type = type,

				FieldInfo = fi.FieldBuilder,
				IsStatic = fi.IsStatic,
				FieldType = fi.FieldBuilder.FieldType
			};
		}

		/// <summary>
		/// Resolves a property from a type by its name.
		/// </summary>
		public PropertyWrapper ResolveProperty(Type type, string name)
		{
			if (!(type is TypeBuilder))
				return ReflectionHelper.ResolveProperty(type, name);

			// no internal properties
			throw new KeyNotFoundException();
		}

		/// <summary>
		/// Resolves a constructor from a type by the list of arguments.
		/// </summary>
		public ConstructorWrapper ResolveConstructor(Type type, Type[] argTypes)
		{
			if (!(type is TypeBuilder))
				return ReflectionHelper.ResolveConstructor(type, argTypes);

			var typeEntity = _DefinedTypes[type.Name];
			var ctor = typeEntity.ResolveConstructor(argTypes);

			return new ConstructorWrapper
			{
				Type = type,
				ConstructorInfo = ctor.ConstructorBuilder,
				ArgumentTypes = ctor.GetArgumentTypes(this),

				IsPartiallyApplied = ReflectionHelper.IsPartiallyApplied(argTypes),
				IsVariadic = false // built-in ctors can't do that
			};
		}

		/// <summary>
		/// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
		/// Generic arguments whose values can be inferred from argument types can be skipped.
		/// </summary>
		public MethodWrapper ResolveMethod(Type type, string name, Type[] argTypes, Type[] hints = null, LambdaResolver resolver = null)
		{
			if (!(type is TypeBuilder))
				return ReflectionHelper.ResolveMethod(type, name, argTypes, hints, resolver);

			var typeEntity = _DefinedTypes[type.Name];
			try
			{
				var method = typeEntity.ResolveMethod(name, argTypes);
				var mw = wrapMethod(method, ReflectionHelper.IsPartiallyApplied(argTypes));

				var isGeneric = method.IsImported && method.MethodInfo.IsGenericMethod;
				if (isGeneric)
				{
					var argTypeDefs = method.MethodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
					var genericDefs = method.MethodInfo.GetGenericArguments();
					var genericValues = GenericHelper.ResolveMethodGenericsByArgs(argTypeDefs, argTypes, genericDefs, hints);

					mw.MethodInfo = method.MethodInfo.MakeGenericMethod(genericValues);
					mw.ArgumentTypes = method.GetArgumentTypes(this).Select(t => GenericHelper.ApplyGenericArguments(t, genericDefs, genericValues)).ToArray();
					mw.GenericArguments = genericValues;
					mw.ReturnType = GenericHelper.ApplyGenericArguments(method.MethodInfo.ReturnType, genericDefs, genericValues);
				}
				else
				{
					if (hints != null)
						Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
				}

				return mw;
			}
			catch (KeyNotFoundException)
			{
				return ResolveMethod(type.BaseType, name, argTypes, hints);
			}
		}

		/// <summary>
		/// Resolves a method within the type, assuming it's the only one with such name.
		/// </summary>
		public MethodWrapper ResolveMethod(Type type, string name, Func<IEnumerable<MethodWrapper>, MethodWrapper> filter = null)
		{
			var group = ResolveMethodGroup(type, name);
			return filter == null
				? group.Single()
				: filter(group);
		}

		/// <summary>
		/// Finds an extension method for current type.
		/// </summary>
		public MethodWrapper ResolveExtensionMethod(Type type, string name, Type[] argTypes, Type[] hints = null, LambdaResolver lambdaResolver = null)
		{
			return ReflectionHelper.ResolveExtensionMethod(_ExtensionResolver, type, name, argTypes, hints, lambdaResolver);
		}

		/// <summary>
		/// Resolves a group of methods by name.
		/// Only non-generic methods are returned!
		/// </summary>
		public IEnumerable<MethodWrapper> ResolveMethodGroup(Type type, string name)
		{
			if (!(type is TypeBuilder))
				return ReflectionHelper.ResolveMethodGroup(type, name);

			var typeEntity = _DefinedTypes[type.Name];
			return typeEntity.ResolveMethodGroup(name).Select(x => wrapMethod(x));
		}

		/// <summary>
		/// Resolves a conversion operator to a certain type.
		/// </summary>
		public MethodWrapper ResolveConvertorToType(Type from, Type to)
		{
			return ResolveMethodGroup(@from, "op_Explicit").FirstOrDefault(x => x.ReturnType == to)
				   ?? ResolveMethodGroup(@from, "op_Implicit").FirstOrDefault(x => x.ReturnType == to);
		}

		/// <summary>
		/// Resolves a global property by its name.
		/// </summary>
		internal GlobalPropertyInfo ResolveGlobalProperty(string name)
		{
			GlobalPropertyInfo ent;
			if (!_DefinedProperties.TryGetValue(name, out ent))
				throw new KeyNotFoundException();

			return ent;
		}

		#region Helpers

		/// <summary>
		/// Resolves a lambda return type when its argument types have been inferred from usage.
		/// </summary>
		public Type ResolveLambda(LambdaNode lambda, Type[] argTypes)
		{
			lambda.SetInferredArgumentTypes(argTypes);
			var delegateType = lambda.Resolve(this);
			return ReflectionHelper.WrapDelegate(delegateType).ReturnType;
		}

		/// <summary>
		/// Creates a wrapper from a method entity.
		/// </summary>
		private MethodWrapper wrapMethod(MethodEntity method, bool isPartial = false)
		{
			return new MethodWrapper
			{
				Name = method.Name,
				Type = method.ContainerType.TypeInfo,

				IsStatic = method.IsStatic,
				IsVirtual = method.IsVirtual,
				IsPartiallyApplied = isPartial,
				IsVariadic = method.IsVariadic,

				MethodInfo = method.MethodInfo,
				ArgumentTypes = method.GetArgumentTypes(this),
				ReturnType = method.ReturnType
			};
		}

		#endregion
	}
}
