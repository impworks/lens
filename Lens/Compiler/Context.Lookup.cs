using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
	partial class Context
	{
		/// <summary>
		/// Finds a declared type by its name.
		/// </summary>
		internal TypeEntity FindType(string name)
		{
			TypeEntity type;
			return _DefinedTypes.TryGetValue(name, out type) ? type : null;
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
		public Type ResolveType(TypeSignature signature)
		{
			var local = FindType(signature.FullSignature);
			return local != null
				? local.TypeInfo
				: _TypeResolver.ResolveType(signature);
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
					Name = name,
					Type = type,

					FieldInfo = field,
					IsStatic = field.IsStatic, 
					IsLiteral = field.IsLiteral,
					FieldType = field.FieldType
				};
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

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
					IsLiteral = genField.IsLiteral,
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
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

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
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

				var genType = type.GetGenericTypeDefinition();
				var genCtor = ResolveMethodByArgs(
					genType.GetConstructors(),
					c => c.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = TypeBuilder.GetConstructor(type, genCtor.Item1),
					ArgumentTypes = genCtor.Item3
				};
			}
		}

		/// <summary>
		/// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
		/// Generic arguments whose values can be inferred from argument types can be skipped.
		/// </summary>
		public MethodWrapper ResolveMethod(Type type, string name, Type[] argTypes, Type[] hints = null)
		{
			return type is TypeBuilder
				       ? resolveInternalMethod(type, name, argTypes, hints)
				       : resolveExternalMethod(type, name, argTypes, hints);
		}

		private MethodWrapper resolveInternalMethod(Type type, string name, Type[] argTypes, Type[] hints)
		{
			var typeEntity = _DefinedTypes[type.Name];
			try
			{
				var method = typeEntity.ResolveMethod(name, argTypes);

				var mw = new MethodWrapper
				{
					Name = name,
					Type = type,

					IsStatic = method.IsStatic,
					IsVirtual = method.IsVirtual
				};

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

					mw.MethodInfo = method.MethodInfo;
					mw.ArgumentTypes = method.GetArgumentTypes(this);
					mw.ReturnType = method.ReturnType;
				}

				return mw;
			}
			catch (KeyNotFoundException)
			{
				return ResolveMethod(type.BaseType, name, argTypes, hints);
			}
		}

		private MethodWrapper resolveExternalMethod(Type type, string name, Type[] argTypes, Type[] hints)
		{
			var mw = new MethodWrapper { Name = name, Type = type };
			var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

			try
			{
				var method = ResolveMethodByArgs(
					type.GetMethods(flags).Where(m => m.Name == name),
					m => m.GetParameters().Select(p => p.ParameterType).ToArray(),
					argTypes
				);

				var mInfo = method.Item1;
				var expectedTypes = method.Item3;

				if (mInfo.IsGenericMethod)
				{
					var genericDefs = mInfo.GetGenericArguments();
					var genericValues = GenericHelper.ResolveMethodGenericsByArgs(expectedTypes, argTypes, genericDefs, hints);

					mInfo = mInfo.MakeGenericMethod(genericValues);
					mw.GenericArguments = genericValues;
				}
				else if (hints != null)
				{
					Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
				}

				mw.MethodInfo = mInfo;
				mw.IsStatic = mInfo.IsStatic;
				mw.IsVirtual = mInfo.IsVirtual;
				mw.ArgumentTypes = expectedTypes;
				mw.ReturnType = mInfo.ReturnType;

				return mw;
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

				var genType = type.GetGenericTypeDefinition();
				var genMethod = ResolveMethodByArgs(
					genType.GetMethods(flags).Where(m => m.Name == name),
					m => m.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type, false)).ToArray(),
					argTypes
				);

				var mInfoOriginal = genMethod.Item1;
				var mInfo = TypeBuilder.GetMethod(type, genMethod.Item1);
				var expectedTypes = genMethod.Item3;

				if (mInfoOriginal.IsGenericMethod)
				{
					var genericDefs = mInfoOriginal.GetGenericArguments();
					var genericValues = GenericHelper.ResolveMethodGenericsByArgs(expectedTypes, argTypes, genericDefs, hints);

					mInfo = mInfo.MakeGenericMethod(genericValues);

					var totalGenericDefs = genericDefs.Union(genType.GetGenericTypeDefinition().GetGenericArguments()).ToArray();
					var totalGenericValues = genericValues.Union(type.GetGenericArguments()).ToArray();

					mw.GenericArguments = genericValues;
					mw.ReturnType = GenericHelper.ApplyGenericArguments(mInfoOriginal.ReturnType, totalGenericDefs, totalGenericValues);
					mw.ArgumentTypes = mInfoOriginal.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, totalGenericDefs, totalGenericValues)).ToArray();
				}
				else 
				{
					if (hints != null)
						Error(CompilerMessages.GenericArgsToNonGenericMethod, name);

					mw.ArgumentTypes = mInfoOriginal.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray();
					mw.ReturnType = GenericHelper.ApplyGenericArguments(mInfoOriginal.ReturnType, type, false);
				}

				mw.MethodInfo = mInfo;
				mw.IsStatic = mInfoOriginal.IsStatic;
				mw.IsVirtual = mInfoOriginal.IsVirtual;
			}

			return mw;
		}

		/// <summary>
		/// Resolves a method within the type, assuming it's the only one with such name.
		/// </summary>
		public MethodWrapper ResolveMethod(Type type, string name)
		{
			return ResolveMethodGroup(type, name).Single();
		}

		/// <summary>
		/// Finds an extension method for current type.
		/// </summary>
		public MethodWrapper ResolveExtensionMethod(Type type, string name, Type[] argTypes, Type[] hints = null)
		{
			var method = _ExtensionResolver.FindExtensionMethod(type, name, argTypes);
			var info = new MethodWrapper
			{
				Name = name,
				Type = method.DeclaringType,

				MethodInfo = method,
				IsStatic = true,
				IsVirtual = false,
				ReturnType = method.ReturnType,
				ArgumentTypes = method.GetParameters().Select(p => p.ParameterType).ToArray()
			};

			if (method.IsGenericMethod)
			{
				var expectedTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
				var genericDefs = method.GetGenericArguments();

				var extMethodArgs = argTypes.ToList();
				extMethodArgs.Insert(0, type);

				var genericValues = GenericHelper.ResolveMethodGenericsByArgs(
					expectedTypes,
					extMethodArgs.ToArray(),
					genericDefs,
					hints
				);

				info.GenericArguments = genericValues;
				info.MethodInfo = info.MethodInfo.MakeGenericMethod(genericValues);
				info.ReturnType = GenericHelper.ApplyGenericArguments(info.ReturnType, genericDefs, genericValues);
				info.ArgumentTypes = expectedTypes.Select(t => GenericHelper.ApplyGenericArguments(t, genericDefs, genericValues)).ToArray();
			}
			else if (hints != null)
			{
				Error(CompilerMessages.GenericArgsToNonGenericMethod, name);
			}

			return info;
		}

		/// <summary>
		/// Resolves a group of methods by name.
		/// Only non-generic methods are returned!
		/// </summary>
		public IEnumerable<MethodWrapper> ResolveMethodGroup(Type type, string name)
		{
			try
			{
				return type.GetMethods().Where(m => !m.IsGenericMethod && m.Name == name).Select(m => new MethodWrapper(m));
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw;

				var genType = type.GetGenericTypeDefinition();
				var genericMethods = genType.GetMethods().Where(m => !m.IsGenericMethod && m.Name == name);

				return genericMethods.Select(
					m => new MethodWrapper
					{
						Name = name,
						Type = type,

						MethodInfo = TypeBuilder.GetMethod(type, m),
						IsStatic = m.IsStatic,
						IsVirtual = m.IsVirtual,
						ArgumentTypes = m.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
						ReturnType = GenericHelper.ApplyGenericArguments(m.ReturnType, type)
					}
				);
			}
		}

		/// <summary>
		/// Resolves an indexer property from a type by its argument.
		/// </summary>
		public MethodWrapper ResolveIndexer(Type type, Type idxType, bool isGetter)
		{
			if(type is TypeBuilder)
				throw new NotSupportedException();

			try
			{
				var indexer = resolveIndexer(type, idxType, isGetter, p => p);
				return new MethodWrapper(indexer);
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw;

				var genType = type.GetGenericTypeDefinition();
				var indexer = resolveIndexer(genType, idxType, isGetter, p => GenericHelper.ApplyGenericArguments(p, type));
				return new MethodWrapper
				{
					Type = type,

					MethodInfo = TypeBuilder.GetMethod(type, indexer),
					IsStatic = false,
					IsVirtual = indexer.IsVirtual,
					ArgumentTypes = indexer.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					ReturnType = GenericHelper.ApplyGenericArguments(indexer.ReturnType, type)
				};
			}
		}

		private MethodInfo resolveIndexer(Type type, Type idxType, bool isGetter, Func<Type, Type> typeProcessor)
		{
			var indexers = new List<Tuple<PropertyInfo, Type, int>>();

			foreach (var pty in type.GetProperties())
			{
				if (isGetter && pty.GetGetMethod() == null)
					continue;

				if (!isGetter && pty.GetSetMethod() == null)
					continue;

				var idxArgs = pty.GetIndexParameters();
				if (idxArgs.Length != 1)
					continue;

				var argType = typeProcessor(idxArgs[0].ParameterType);
				var distance = argType.DistanceFrom(idxType);

				indexers.Add(new Tuple<PropertyInfo, Type, int>(pty, argType, distance));
			}

			indexers.Sort((x, y) => x.Item3.CompareTo(y.Item3));

			if(indexers.Count == 0 || indexers[0].Item3 == int.MaxValue)
				Error(
					isGetter ? CompilerMessages.IndexGetterNotFound : CompilerMessages.IndexSetterNotFound,
					type,
					idxType
				);

			if (indexers.Count > 1 && indexers[0].Item3 == indexers[1].Item3)
				Error(
					CompilerMessages.IndexAmbigious,
					type,
					indexers[0].Item2,
					indexers[1].Item2,
					Environment.NewLine
				);

			var it = indexers[0];

			return isGetter ? it.Item1.GetGetMethod() : it.Item1.GetSetMethod();
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

			var result = list.Select(methodEvaluator).OrderBy(rec => rec.Item2).Take(2).ToArray();

			if (result.Length == 0 || result[0].Item2 == int.MaxValue)
				throw new KeyNotFoundException();

			if (result.Length == 2 && result[0].Item2 == result[1].Item2)
				throw new AmbiguousMatchException();

			return result[0];
		}

		/// <summary>
		/// Gets the information about a delegate by its type.
		/// </summary>
		public MethodWrapper WrapDelegate(Type type)
		{
			if(!type.IsCallableType())
				throw new ArgumentException("type");

			return ResolveMethod(type, "Invoke");
		}

		/// <summary>
		/// Checks if two delegates can be combined.
		/// </summary>
		public bool CanCombineDelegates(Type left, Type right)
		{
			if (!left.IsCallableType() || !right.IsCallableType())
				return false;

			var rt = WrapDelegate(left).ReturnType;
			var args = WrapDelegate(right).ArgumentTypes;

			return args.Count() == 1 && args[0].IsAssignableFrom(rt);
		}

		/// <summary>
		/// Creates a new delegate that combines the two given ones.
		/// </summary>
		public Type CombineDelegates(Type left, Type right)
		{
			if (!left.IsCallableType() || !right.IsCallableType())
				return null;

			var args = WrapDelegate(left).ArgumentTypes;
			var rt = WrapDelegate(right).ReturnType;

			return FunctionalHelper.CreateDelegateType(rt, args);
		}
	}
}
