using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Lens.SyntaxTree.Compiler
{
	partial class Context
	{
		/// <summary>
		/// Resolves a type by its string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			try
			{
				TypeEntity type;
				return _DefinedTypes.TryGetValue(signature, out type)
					? type.TypeInfo
					: _TypeResolver.ResolveType(signature);
			}
			catch (ArgumentException ex)
			{
				throw new LensCompilerException(ex.Message);
			}
		}

		/// <summary>
		/// Resolves a type by its signature.
		/// </summary>
		public Type ResolveType(TypeSignature signature)
		{
			try
			{
				return ResolveType(signature.Signature);
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(signature);
				throw;
			}
		}

		/// <summary>
		/// Resolves a field from a type by its name.
		/// </summary>
		public FieldWrapper ResolveField(Type type, string name)
		{
			if (type is TypeBuilder)
			{
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

			try
			{
				var field = type.GetField(name);
				if(field == null)
					throw new KeyNotFoundException();

				return new FieldWrapper
				{
					FieldInfo = field,
					IsStatic = field.IsStatic, 
					FieldType = field.FieldType
				};
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genField = genType.GetField(name);

				if (genField == null)
					throw new KeyNotFoundException();

				return new FieldWrapper
				{
					Name = name,
					Type = type,

					FieldInfo = TypeBuilder.GetField(type, genField),
					IsStatic =  genField.IsStatic,
					FieldType = GenericHelper.ApplyGenericArguments(genField.FieldType, type)
				};
			}
		}

		/// <summary>
		/// Resolves a property from a type by its name.
		/// </summary>
		public PropertyWrapper ResolveProperty(Type type, string name)
		{
			// no internal properties
			if(type is TypeBuilder)
				throw new KeyNotFoundException();

			try
			{
				var pty = type.GetProperty(name);
				if (pty == null)
					throw new KeyNotFoundException();

				return new PropertyWrapper
				{
					Name = name,
					Type = type,

					Getter = pty.GetGetMethod(),
					Setter = pty.GetSetMethod(),
					IsStatic = (pty.GetGetMethod() ?? pty.GetSetMethod()).IsStatic,
					PropertyType = pty.PropertyType
				};
			}
			catch(NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genPty = genType.GetProperty(name);

				if (genPty == null)
					throw new KeyNotFoundException();

				var getter = genPty.GetGetMethod();
				var setter = genPty.GetSetMethod();

				return new PropertyWrapper
				{
					Name = name,
					Type = type,
					
					Getter = getter == null ? null : TypeBuilder.GetMethod(type, getter),
					Setter = setter == null ? null : TypeBuilder.GetMethod(type, setter),
					IsStatic = (getter ?? setter).IsStatic,
					PropertyType = GenericHelper.ApplyGenericArguments(genPty.PropertyType, type),
				};
			}
		}

		/// <summary>
		/// Resolves a constructor from a type by the list of arguments.
		/// </summary>
		public ConstructorWrapper ResolveConstructor(Type type, Type[] argTypes)
		{
			if (type is TypeBuilder)
			{
				var typeEntity = _DefinedTypes[type.Name];
				var ctor = typeEntity.ResolveConstructor(argTypes);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = ctor.ConstructorBuilder,
					ArgumentTypes = ctor.GetArgumentTypes(this)
				};
			}

			try
			{
				var ctor = ResolveMethodByArgs(
					type.GetConstructors(), 
					c => c.GetParameters().Select(p => p.ParameterType).ToArray(),
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = ctor.Item1,
					ArgumentTypes = ctor.Item1.GetParameters().Select(p => p.ParameterType).ToArray()
				};
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genCtor = ResolveMethodByArgs(
					genType.GetConstructors(),
					c => c.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = genCtor.Item1,
					ArgumentTypes = genCtor.Item3;
				};
			}
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

		/// <summary>
		/// Resolves the best-matching method-like entity within a generic list.
		/// </summary>
		/// <typeparam name="T">Type of method-like entity.</typeparam>
		/// <param name="list">List of method-like entitites.</param>
		/// <param name="argsGetter">A function that gets method entity arguments.</param>
		/// <param name="args">Desired argument types.</param>
		public static Tuple<T, int, Type[]> ResolveMethodByArgs<T>(IEnumerable<T> list, Func<T, Type[]> argsGetter, Type[] args)
		{
			Func<T, Tuple<T, int, Type[]>> methodEvaluator = ent =>
			{
				var currArgs = argsGetter(ent);
				var dist = ExtensionMethodResolver.GetArgumentsDistance(args, currArgs);
				return new Tuple<T, int, Type[]>(ent, dist, currArgs);
			};

			var result = list.Select(methodEvaluator).OrderBy(rec => rec.Item2).ToArray();

			if (result.Length == 0 || result[0].Item2 == int.MaxValue)
				throw new KeyNotFoundException("No suitable method was found!");

			if (result.Length > 2)
			{
				var ambiCount = result.Skip(1).TakeWhile(i => i.Item2 == result[0].Item2).Count();
				if (ambiCount > 0)
					throw new AmbiguousMatchException();
			}

			return result[0];
		}
	}
}
