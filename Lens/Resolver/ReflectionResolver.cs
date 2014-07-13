using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Resolver
{
	/// <summary>
	/// Reflection + Generic helper.
	/// </summary>
	internal class ReflectionResolver
	{
		#region Constructor

		public ReflectionResolver(Dictionary<Type, TypeDetails> typeLookup = null)
		{
			_Lookup = typeLookup ?? new Dictionary<Type, TypeDetails>();
			m_DistanceCache = new Dictionary<Tuple<Type, Type, bool>, int>();
			m_InterfaceCache = new Dictionary<Type, Type[]>();
		}

		#endregion

		#region Fields

		/// <summary>
		/// Lookup function for assembly-defined types;
		/// </summary>
		private Dictionary<Type, TypeDetails> _Lookup;

		/// <summary>
		///  Type distance memoization cache.
		/// </summary>
		private Dictionary<Tuple<Type, Type, bool>, int> m_DistanceCache;

		/// <summary>
		/// Type interface memoization cache.
		/// </summary>
		private readonly Dictionary<Type, Type[]> m_InterfaceCache;

		#endregion

		#region Various resolvers

		/// <summary>
		/// Resolves a field from a type by its name.
		/// </summary>
		public FieldWrapper ResolveField(Type type, string name)
		{
			try
			{
				var field = type.GetField(name);
				if (field == null)
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
					IsStatic = genField.IsStatic,
					IsLiteral = genField.IsLiteral,
					FieldType = ApplyGenericArguments(genField.FieldType, type)
				};
			}
		}

		/// <summary>
		/// Resolves a property from a type by its name.
		/// </summary>
		public PropertyWrapper ResolveProperty(Type type, string name)
		{
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
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

				var genType = type.GetGenericTypeDefinition();
				var genPty = genType.GetProperty(name);

				if (genPty == null)
					throw new KeyNotFoundException();

				var getter = genPty.GetGetMethod();
				var setter = genPty.GetSetMethod();

				var declType = resolveActualDeclaringType(type, genPty.DeclaringType);

				return new PropertyWrapper
				{
					Name = name,
					Type = type,

					Getter = getMethodVersionForType(declType, getter),
					Setter = getMethodVersionForType(declType, setter),
					IsStatic = (getter ?? setter).IsStatic,
					PropertyType = ApplyGenericArguments(genPty.PropertyType, type),
				};
			}
		}

		/// <summary>
		/// Resolves a constructor from a type by the list of arguments.
		/// </summary>
		public ConstructorWrapper ResolveConstructor(Type type, Type[] argTypes)
		{
			try
			{
				var ctor = ResolveMethodByArgs(
					type.GetConstructors(),
					c => c.GetParameters().Select(p => p.ParameterType).ToArray(),
					IsVariadic,
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = ctor.Method,
					ArgumentTypes = ctor.ArgumentTypes,
					IsPartiallyApplied = IsPartiallyApplied(argTypes),
					IsVariadic = IsVariadic(ctor.Method)
				};
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

				var genType = type.GetGenericTypeDefinition();
				var genCtor = ResolveMethodByArgs(
					genType.GetConstructors(),
					c => c.GetParameters().Select(p => ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					IsVariadic,
					argTypes
				);

				return new ConstructorWrapper
				{
					Type = type,
					ConstructorInfo = TypeBuilder.GetConstructor(type, genCtor.Method),
					ArgumentTypes = genCtor.ArgumentTypes,
					IsPartiallyApplied = IsPartiallyApplied(argTypes),
					IsVariadic = IsVariadic(genCtor.Method)
				};
			}
		}

		/// <summary>
		/// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
		/// Generic arguments whose values can be inferred from argument types can be skipped.
		/// </summary>
		public MethodWrapper ResolveMethod(Type type, string name, Type[] argTypes, Type[] hints, LambdaResolver lambdaResolver)
		{
			var mw = new MethodWrapper { Name = name, Type = type };

			try
			{
				var method = ResolveMethodByArgs(
					getMethodsByName(type, name),
					m => m.GetParameters().Select(p => p.ParameterType).ToArray(),
					IsVariadic,
					argTypes
				);

				var mInfo = method.Method;

				if (mInfo.IsGenericMethod)
				{
					var genericDefs = mInfo.GetGenericArguments();
					var genericValues = ResolveMethodGenericsByArgs(method.ArgumentTypes, argTypes, genericDefs, hints);

					mInfo = mInfo.MakeGenericMethod(genericValues);
					mw.GenericArguments = genericValues;
				}
				else if (hints != null)
				{
					error(CompilerMessages.GenericArgsToNonGenericMethod, name);
				}

				mw.MethodInfo = mInfo;
				mw.IsStatic = mInfo.IsStatic;
				mw.IsVirtual = mInfo.IsVirtual;
				mw.ArgumentTypes = method.ArgumentTypes;
				mw.ReturnType = mInfo.ReturnType;
				mw.IsPartiallyApplied = IsPartiallyApplied(argTypes);
				mw.IsVariadic = IsVariadic(mInfo);

				return mw;
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

				var genType = type.GetGenericTypeDefinition();
				var genMethod = ResolveMethodByArgs(
					getMethodsByName(genType, name),
					m => m.GetParameters().Select(p => ApplyGenericArguments(p.ParameterType, type, false)).ToArray(),
					IsVariadic,
					argTypes
				);

				var mInfoOriginal = genMethod.Method;
				var declType = resolveActualDeclaringType(type, mInfoOriginal.DeclaringType);
				var mInfo = getMethodVersionForType(declType, mInfoOriginal);

				if (mInfoOriginal.IsGenericMethod)
				{
					var genericDefs = mInfoOriginal.GetGenericArguments();
					var genericValues = ResolveMethodGenericsByArgs(genMethod.ArgumentTypes, argTypes, genericDefs, hints, lambdaResolver);

					mInfo = mInfo.MakeGenericMethod(genericValues);

					var totalGenericDefs = genericDefs.Union(genType.GetGenericTypeDefinition().GetGenericArguments()).ToArray();
					var totalGenericValues = genericValues.Union(type.GetGenericArguments()).ToArray();

					mw.GenericArguments = genericValues;
					mw.ReturnType = ApplyGenericArguments(mInfoOriginal.ReturnType, totalGenericDefs, totalGenericValues);
					mw.ArgumentTypes = mInfoOriginal.GetParameters().Select(p => ApplyGenericArguments(p.ParameterType, totalGenericDefs, totalGenericValues)).ToArray();
				}
				else
				{
					if (hints != null)
						error(CompilerMessages.GenericArgsToNonGenericMethod, name);

					mw.ArgumentTypes = mInfoOriginal.GetParameters().Select(p => ApplyGenericArguments(p.ParameterType, type)).ToArray();
					mw.ReturnType = ApplyGenericArguments(mInfoOriginal.ReturnType, type, false);
				}

				mw.MethodInfo = mInfo;
				mw.IsStatic = mInfoOriginal.IsStatic;
				mw.IsVirtual = mInfoOriginal.IsVirtual;
				mw.IsPartiallyApplied = IsPartiallyApplied(argTypes);
				mw.IsVariadic = IsVariadic(mInfoOriginal);
			}

			return mw;
		}

		/// <summary>
		/// Resolves an extension method by arguments.
		/// </summary>
		public MethodWrapper ResolveExtensionMethod(ExtensionMethodResolver resolver, Type type, string name, Type[] argTypes, Type[] hints, LambdaResolver lambdaResolver)
		{
			var method = resolver.ResolveExtensionMethod(type, name, argTypes);
			var args = method.GetParameters();
			var info = new MethodWrapper
			{
				Name = name,
				Type = method.DeclaringType,

				MethodInfo = method,
				IsStatic = true,
				IsVirtual = false,
				ReturnType = method.ReturnType,
				ArgumentTypes = args.Select(p => p.ParameterType).ToArray(),
				IsPartiallyApplied = IsPartiallyApplied(argTypes),
				IsVariadic = IsVariadic(method),
			};

			if (method.IsGenericMethod)
			{
				var expectedTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
				var genericDefs = method.GetGenericArguments();

				var extMethodArgs = argTypes.ToList();
				extMethodArgs.Insert(0, type);

				var genericValues = ResolveMethodGenericsByArgs(
					expectedTypes,
					extMethodArgs.ToArray(),
					genericDefs,
					hints,
					lambdaResolver
				);

				info.GenericArguments = genericValues;
				info.MethodInfo = info.MethodInfo.MakeGenericMethod(genericValues);
				info.ReturnType = ApplyGenericArguments(info.ReturnType, genericDefs, genericValues);
				info.ArgumentTypes = expectedTypes.Select(t => ApplyGenericArguments(t, genericDefs, genericValues)).ToArray();
			}
			else if (hints != null)
			{
				error(CompilerMessages.GenericArgsToNonGenericMethod, name);
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
				return getMethodsByName(type, name).Where(m => !m.IsGenericMethod).Select(m => new MethodWrapper(m));
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw;

				var genType = type.GetGenericTypeDefinition();
				var genericMethods = getMethodsByName(genType, name).Where(m => !m.IsGenericMethod).ToArray();

				return genericMethods.Select(
					m =>
					{
						var declType = resolveActualDeclaringType(type, m.DeclaringType);
						return new MethodWrapper
						{
							Name = name,
							Type = type,

							MethodInfo = getMethodVersionForType(declType, m),
							IsStatic = m.IsStatic,
							IsVirtual = m.IsVirtual,
							ArgumentTypes = m.GetParameters().Select(p => ApplyGenericArguments(p.ParameterType, declType)).ToArray(),
							ReturnType = ApplyGenericArguments(m.ReturnType, declType)
						};
					}
				);
			}
		}


		/// <summary>
		/// Resolves an indexer property from a type by its argument.
		/// </summary>
		public MethodWrapper ResolveIndexer(Type type, Type idxType, bool isGetter)
		{
			if (type is TypeBuilder)
				throw new NotSupportedException();

			try
			{
				var indexer = resolveIndexerProperty(type, idxType, isGetter, p => p);
				return new MethodWrapper(indexer);
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw;

				var genType = type.GetGenericTypeDefinition();
				var indexer = resolveIndexerProperty(genType, idxType, isGetter, p => ApplyGenericArguments(p, type));
				var declType = resolveActualDeclaringType(type, indexer.DeclaringType);

				return new MethodWrapper
				{
					Type = type,

					MethodInfo = getMethodVersionForType(declType, indexer),
					IsStatic = false,
					IsVirtual = indexer.IsVirtual,
					ArgumentTypes = indexer.GetParameters().Select(p => ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					ReturnType = ApplyGenericArguments(indexer.ReturnType, type)
				};
			}
		}

		/// <summary>
		/// Finds a property that can work as an index.
		/// </summary>
		private MethodInfo resolveIndexerProperty(Type type, Type idxType, bool isGetter, Func<Type, Type> typeProcessor)
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
				var distance = DistanceFrom(argType, idxType);

				indexers.Add(new Tuple<PropertyInfo, Type, int>(pty, argType, distance));
			}

			indexers.Sort((x, y) => x.Item3.CompareTo(y.Item3));

			if (indexers.Count == 0 || indexers[0].Item3 == int.MaxValue)
				error(
					isGetter ? CompilerMessages.IndexGetterNotFound : CompilerMessages.IndexSetterNotFound,
					type,
					idxType
				);

			if (indexers.Count > 1 && indexers[0].Item3 == indexers[1].Item3)
				error(
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
		/// Resolves the best-matching method-like entity within a generic list.
		/// </summary>
		/// <typeparam name="T">Type of method-like entity.</typeparam>
		/// <param name="list">List of method-like entitites.</param>
		/// <param name="argsGetter">A function that gets method entity arguments.</param>
		/// <param name="argTypes">Desired argument types.</param>
		public MethodLookupResult<T> ResolveMethodByArgs<T>(IEnumerable<T> list, Func<T, Type[]> argsGetter, Func<T, bool> isVariadicGetter, Type[] argTypes)
		{
			var result = list.Select(x => ArgumentDistance(argTypes, argsGetter(x), x, isVariadicGetter(x)))
							 .OrderBy(rec => rec.Distance)
							 .Take(2) // no more than 2 is needed
							 .ToArray();

			if (result.Length == 0 || result[0].Distance == int.MaxValue)
				throw new KeyNotFoundException();

			if (result.Length == 2 && result[0].Distance == result[1].Distance)
				throw new AmbiguousMatchException();

			return result[0];
		}

		#endregion

		#region Interface resolver

		/// <summary>
		/// Get interfaces of a possibly generic type.
		/// </summary>
		public Type[] ResolveInterfaces(Type type)
		{
			if (m_InterfaceCache.ContainsKey(type))
				return m_InterfaceCache[type].ToArray();

			Type[] ifaces;
			try
			{
				ifaces = type.GetInterfaces();
			}
			catch (NotSupportedException)
			{
				if (type.IsGenericType)
				{
					ifaces = type.GetGenericTypeDefinition().GetInterfaces();
					for (var idx = 0; idx < ifaces.Length; idx++)
					{
						var curr = ifaces[idx];
						if (curr.IsGenericType)
							ifaces[idx] = ApplyGenericArguments(curr, type);
					}
				}

				else if (type.IsArray)
				{
					// replace interfaces of any array with element type
					var elem = type.GetElementType();
					ifaces = typeof(int[]).GetInterfaces();
					for (var idx = 0; idx < ifaces.Length; idx++)
					{
						var curr = ifaces[idx];
						if (curr.IsGenericType)
							ifaces[idx] = curr.GetGenericTypeDefinition().MakeGenericType(elem);
					}
				}

				// just a built-in type : no interfaces
				else
				{
					ifaces = Type.EmptyTypes;
				}
			}

			m_InterfaceCache.Add(type, ifaces);
			return ifaces.ToArray();
		}

		#endregion

		#region Delegate handling

		/// <summary>
		/// Gets the information about a delegate by its type.
		/// </summary>
		public MethodWrapper WrapDelegate(Type type)
		{
			if (!type.IsCallableType())
				throw new ArgumentException("type");

			return ResolveMethodGroup(type, "Invoke").Single();
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

		#endregion

		#region Checkers

		/// <summary>
		/// Checks if method can accept an arbitrary amount of arguments.
		/// </summary>
		public bool IsVariadic(MethodBase method)
		{
			// todo: use dynamic method
			var args = method.GetParameters();
			return args.Length > 0 && args[args.Length - 1].IsDefined(typeof(ParamArrayAttribute), true);
		}

		/// <summary>
		/// Checks if the list of argument types denotes a partial application case.
		/// </summary>
		public bool IsPartiallyApplied(Type[] argTypes)
		{
			return argTypes.Contains(typeof(UnspecifiedType));
		}

		/// <summary>
		/// Checks if the list of argument types contains a lambda expression with unresolved argument types.
		/// </summary>
		public bool IsPartiallyResolved(Type[] argTypes)
		{
			for (var idx = 0; idx < argTypes.Length; idx++)
				if (argTypes[idx].IsLambdaType())
					return true;

			return false;
		}

		/// <summary>
		/// Checks if the possibly generic type has a default constructor.
		/// </summary>
		public bool HasDefaultConstructor(Type type)
		{
			if (type.IsValueType)
				return true;

			try
			{
				return type.GetConstructor(Type.EmptyTypes) != null;
			}
			catch (NotSupportedException)
			{
				if (type.IsGenericType)
					return HasDefaultConstructor(type.GetGenericTypeDefinition());

				// arrays do not have constructors
				if (type.IsArray)
					return false;

				// type labels and records have constructors
				return true;
			}
		}

		#endregion

		#region Generic helper

		/// <summary>
		/// Resolves the generic values for a specified type.
		/// </summary>
		/// <param name="expectedTypes">Parameter types from method definition.</param>
		/// <param name="actualTypes">Argument types from method invocation site. </param>
		/// <param name="genericDefs">Generic parameters from method definition.</param>
		/// <param name="hints">Extra hints that are specified explicitly.</param>
		/// <param name="lambdaResolver">
		/// Callback for Lambda`T resolution.
		/// Passed arguments are:
		/// 1. Lambda's position in the argument list (to find a corresponding NodeBase)
		/// 2. Already resolved list of types
		/// Return value is the inferred type of lambda return.
		/// </param>
		/// <returns></returns>
		public Type[] ResolveMethodGenericsByArgs(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, Type[] hints = null, LambdaResolver lambdaResolver = null)
		{
			if (hints != null && hints.Length != genericDefs.Length)
				throw new ArgumentException("hints");

			var resolver = new GenericResolver(this, genericDefs, hints, lambdaResolver);
			return resolver.Resolve(expectedTypes, actualTypes);
		}

		/// <summary>
		/// Processes a type and replaces any references of generic arguments inside it with actual values.
		/// </summary>
		/// <param name="type">Type to process.</param>
		/// <param name="source">Type that contains the processed type as a generic parameter.</param>
		public static Type ApplyGenericArguments(Type type, Type source, bool throwNotFound = true)
		{
			// TODO: apply memoization

			if (source.IsGenericType)
			{
				return ApplyGenericArguments(
					type,
					source.GetGenericTypeDefinition().GetGenericArguments(),
					source.GetGenericArguments(),
					throwNotFound
				);
			}

			if (source.IsArray && type.IsGenericType)
			{
				return ApplyGenericArguments(
					type,
					new[] { type.GetGenericArguments()[0] },
					new[] { source.GetElementType() },
					throwNotFound
				);
			}

			return type;
		}

		/// <summary>
		/// Processes a type and replaces any references of generic arguments inside it with actual values.
		/// </summary>
		/// <param name="type">Type to process.</param>
		/// <param name="generics">Generic parameters that can be used in the type.</param>
		/// <param name="values">Actual values of generic parameters.</param>
		public static Type ApplyGenericArguments(Type type, Type[] generics, Type[] values, bool throwNotFound = true)
		{
			if (type.IsArray || type.IsByRef)
			{
				var t = ApplyGenericArguments(type.GetElementType(), generics, values, throwNotFound);
				return type.IsArray ? t.MakeArrayType() : t.MakeByRefType();
			}

			if (type.IsGenericParameter)
			{
				for (var idx = 0; idx < generics.Length; idx++)
				{
					if (generics[idx] == type)
					{
						var result = values[idx];
						if (result == null || result == typeof(UnspecifiedType))
							throw new InvalidOperationException();

						return values[idx];
					}
				}

				if (throwNotFound)
					throw new ArgumentOutOfRangeException(string.Format(CompilerMessages.GenericParameterNotFound, type));

				return type;
			}

			if (type.IsGenericType)
			{
				var def = type.GetGenericTypeDefinition();
				var processed = type.GetGenericArguments().Select(a => ApplyGenericArguments(a, generics, values, throwNotFound)).ToArray();
				return def.MakeGenericType(processed);
			}

			return type;
		}

		/// <summary>
		/// Ensures that actual arguments can be applied to corresponding placeholders.
		/// </summary>
		public Type MakeGenericTypeChecked(Type type, params Type[] values)
		{
			if (!type.IsGenericTypeDefinition)
				return type;

			var args = type.GetGenericArguments();
			if (args.Length != values.Length)
				throw new ArgumentOutOfRangeException("values");

			for (var idx = 0; idx < args.Length; idx++)
			{
				var arg = args[idx];
				var constr = arg.GenericParameterAttributes;
				var value = values[idx];

				if (constr.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && value.IsValueType)
					throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, arg, type));

				if (constr.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
					if (!value.IsValueType || (value.IsGenericType && value.GetGenericTypeDefinition() == typeof(Nullable<>)))
						throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, arg, type));

				if (constr.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !HasDefaultConstructor(value))
					throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, arg, type));

				var bases = arg.GetGenericParameterConstraints();
				foreach (var currBase in bases)
					if (!IsExtendablyAssignableFrom(currBase, value, true))
						throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, arg, type, currBase));
			}

			return type.MakeGenericType(values);
		}

		#endregion

		#region Generic resolver routines

		private class GenericResolver
		{
			public GenericResolver(ReflectionResolver core, Type[] genericDefs, Type[] hints, LambdaResolver lambdaResolver)
			{
				_Core = core;
				_GenericDefs = genericDefs;
				_GenericValues = hints ?? new Type[_GenericDefs.Length];

				_LambdaResolver = lambdaResolver;
			}

			private readonly ReflectionResolver _Core;
			private readonly Type[] _GenericDefs;
			private Type[] _GenericValues;
			private readonly LambdaResolver _LambdaResolver;

			public Type[] Resolve(Type[] expected, Type[] actual)
			{
				resolveRecursive(expected, actual, 0);

				// check if all generics have been resolved
				for (var idx = 0; idx < _GenericDefs.Length; idx++)
					if (_GenericValues[idx] == null)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, _GenericDefs[idx]));

				return _GenericValues;
			}

			/// <summary>
			/// Resolves generic argument values for a method by its argument types.
			/// </summary>
			/// <param name="expectedTypes">Parameter types from method definition.</param>
			/// <param name="actualTypes">Actual types of arguments passed to the parameters.</param>
			/// <param name="depth">Recursion depth for condition checks.</param>
			private void resolveRecursive(Type[] expectedTypes, Type[] actualTypes, int depth)
			{
				var exLen = expectedTypes != null ? expectedTypes.Length : 0;
				var actLen = actualTypes != null ? actualTypes.Length : 0;

				if (exLen != actLen)
					throw new ArgumentException(CompilerMessages.GenericArgCountMismatch);

				for (var idx = 0; idx < exLen; idx++)
				{
					var expected = expectedTypes[idx];
					var actual = actualTypes[idx];

					if (expected.IsGenericType)
					{
						if (actual.IsLambdaType())
						{
							if (depth > 0)
								throw new InvalidOperationException("Lambda expressions cannot be nested!");

							resolveLambda(expected, actual, idx, depth);
						}
						else
						{
							var closest = findImplementation(expected, actual);
							resolveRecursive(
								expected.GetGenericArguments(),
								closest.GetGenericArguments(),
								depth + 1
							);
						}
					}

					else
					{
						for (var defIdx = 0; defIdx < _GenericDefs.Length; defIdx++)
						{
							var def = _GenericDefs[defIdx];
							var value = _GenericValues[defIdx];

							if (expected != def)
								continue;

							if (value != null && value != actual)
								throw new TypeMatchException(string.Format(CompilerMessages.GenericArgMismatch, def, actual, value));

							_GenericValues[defIdx] = actual;
						}
					}
				}
			}

			/// <summary>
			/// Resolves the lambda's input types if they are not specified.
			/// </summary>
			private void resolveLambda(Type expected, Type actual, int lambdaPosition, int depth)
			{
				var expectedInfo = _Core.WrapDelegate(expected);
				var actualInfo = _Core.WrapDelegate(actual);

				var argTypes = new Type[actualInfo.ArgumentTypes.Length];

				// we assume that method has been resolved as matching correctly,
				// therefore no need to double-check argument count & stuff
				for (var idx = 0; idx < expectedInfo.ArgumentTypes.Length; idx++)
				{
					var expArg = expectedInfo.ArgumentTypes[idx];
					var actualArg = actualInfo.ArgumentTypes[idx];

					if (actualArg == typeof(UnspecifiedType))
					{
						// type is unspecified: try to infer it
						try
						{
							argTypes[idx] = ApplyGenericArguments(expArg, _GenericDefs, _GenericValues);
						}
						catch (InvalidOperationException)
						{
							throw new LensCompilerException(string.Format(CompilerMessages.LambdaArgGenericsUnresolved, expArg));
						}
					}
					else
					{
						// type is specified: use it
						argTypes[idx] = actualArg;
						resolveRecursive(
							new[] { expArg },
							new[] { actualArg },
							depth + 1
						);
					}
				}

				if (_LambdaResolver != null)
				{
					var lambdaReturnType = _LambdaResolver(lambdaPosition, argTypes);

					// return type is significant for generic resolution
					if (containsGenericParameter(expectedInfo.ReturnType))
					{
						resolveRecursive(
							new[] { expectedInfo.ReturnType },
							new[] { lambdaReturnType },
							depth + 1
						);
					}
				}
			}

			/// <summary>
			/// Finds the appropriate generic type in the inheritance of the actual type.
			/// </summary>
			private Type findImplementation(Type desired, Type actual)
			{
				var generic = desired.GetGenericTypeDefinition();

				if (actual.IsGenericType && actual.GetGenericTypeDefinition() == generic)
					return actual;

				// is interface
				if (desired.IsInterface)
				{
					var matching = _Core.ResolveInterfaces(actual).Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic).Take(2).ToArray();
					if (matching.Length == 0)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericInterfaceNotImplemented, actual, generic));
					if (matching.Length > 1)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericInterfaceMultipleImplementations, generic, actual));

					return matching[0];
				}

				// is inherited
				var currType = actual;
				while (currType != null)
				{
					if (currType.IsGenericType && currType.GetGenericTypeDefinition() == generic)
						return currType;

					currType = currType.BaseType;
				}

				throw new TypeMatchException(string.Format(CompilerMessages.GenericImplementationWrongType, generic, actual));
			}

			/// <summary>
			/// Recursively checks if the type has a reference to any of the generic argument types.
			/// </summary>
			private static bool containsGenericParameter(Type type)
			{
				if (type.IsGenericParameter)
					return true;

				if (type.IsGenericType && !type.IsGenericTypeDefinition)
				{
					foreach (var curr in type.GetGenericArguments())
						if (containsGenericParameter(curr))
							return true;
				}

				return false;
			}
		}

		#endregion

		#region Type distance

		/// <summary>
		/// Checks if a variable of given type can be assigned from other type (including type extension).
		/// </summary>
		/// <param name="varType">Type of assignment target (ex. variable)</param>
		/// <param name="exprType">Type of assignment source (ex. expression)</param>
		/// <param name="exactly">Checks whether types must be compatible as-is, or additional code may be implicitly issued by the compiler.</param>
		/// <returns></returns>
		public bool IsExtendablyAssignableFrom(Type varType, Type exprType, bool exactly = false)
		{
			return DistanceFrom(varType, exprType, exactly) < int.MaxValue;
		}

		/// <summary>
		/// Gets distance between two types.
		/// This method is memoized.
		/// </summary>
		public int DistanceFrom(Type varType, Type exprType, bool exactly = false)
		{
			var key = new Tuple<Type, Type, bool>(varType, exprType, exactly);

			if (!m_DistanceCache.ContainsKey(key))
				m_DistanceCache.Add(key, distanceFrom(varType, exprType, exactly));

			return m_DistanceCache[key];
		}

		/// <summary>
		/// Calculates the distance between two types.
		/// </summary>
		private int distanceFrom(Type varType, Type exprType, bool exactly = false)
		{
			if (varType == exprType)
				return 0;

			// partial application
			if (exprType == typeof(UnspecifiedType))
				return 0;

			if (varType.IsByRef)
				return varType.GetElementType() == exprType ? 0 : int.MaxValue;

			if (!exactly)
			{
				if (varType.IsNullableType() && exprType == Nullable.GetUnderlyingType(varType))
					return 1;

				if ((varType.IsClass || varType.IsNullableType()) && exprType == typeof(NullType))
					return 1;

				if (varType.IsNumericType(true) && exprType.IsNumericType(true))
					return numericTypeConversion(varType, exprType);
			}

			if (varType == typeof(object))
			{
				if (exprType.IsValueType)
					return exactly ? int.MaxValue : 1;

				if (exprType.IsInterface)
					return 1;
			}

			if (varType.IsInterface)
			{
				if (exprType.IsInterface)
					return interfaceDistance(varType, new[] { exprType }.Union(ResolveInterfaces(exprType)));

				// casting expression to interface takes 1 step
				var dist = interfaceDistance(varType, ResolveInterfaces(exprType));
				if (dist < int.MaxValue)
					return dist + 1;
			}

			if (varType.IsGenericParameter || exprType.IsGenericParameter)
				return genericParameterDistance(varType, exprType);

			if (exprType.IsLambdaType())
				return lambdaDistance(varType, exprType);

			if (varType.IsGenericType && exprType.IsGenericType)
				return genericDistance(varType, exprType);

			int result;
			if (isDerivedFrom(exprType, varType, out result))
				return result;

			if (varType.IsArray && exprType.IsArray)
			{
				var varElType = varType.GetElementType();
				var exprElType = exprType.GetElementType();

				var areRefs = !varElType.IsValueType && !exprElType.IsValueType;
				var generic = varElType.IsGenericParameter || exprElType.IsGenericParameter;
				if (areRefs || generic)
					return DistanceFrom(varElType, exprElType, exactly);
			}

			return int.MaxValue;
		}

		/// <summary>
		/// Calculates the distance to any of given interfaces.
		/// </summary>
		private int interfaceDistance(Type interfaceType, IEnumerable<Type> ifaces, bool exactly = false)
		{
			var min = int.MaxValue;
			foreach (var iface in ifaces)
			{
				if (iface == interfaceType)
					return 0;

				if (interfaceType.IsGenericType && iface.IsGenericType)
				{
					var dist = genericDistance(interfaceType, iface, exactly);
					if (dist < min)
						min = dist;
				}
			}

			return min;
		}

		/// <summary>
		/// Checks if a type is a child for some other type.
		/// </summary>
		private static bool isDerivedFrom(Type derivedType, Type baseType, out int distance)
		{
			distance = 0;
			var current = derivedType;
			while (current != null && current != baseType)
			{
				current = current.BaseType;
				++distance;
			}

			return current == baseType;
		}

		/// <summary>
		/// Calculates compound distance of two generic types' arguments if applicable.
		/// </summary>
		private int genericDistance(Type varType, Type exprType, bool exactly = false)
		{
			var definition = varType.GetGenericTypeDefinition();
			if (definition != exprType.GetGenericTypeDefinition())
				return int.MaxValue;

			var arguments = definition.GetGenericArguments();
			var arguments1 = varType.GetGenericArguments();
			var arguments2 = exprType.GetGenericArguments();

			var result = 0;
			for (var i = 0; i < arguments1.Length; ++i)
			{
				var argument1 = arguments1[i];
				var argument2 = arguments2[i];
				if (argument1 == argument2)
					continue;

				var argument = arguments[i];
				var attributes = argument.GenericParameterAttributes;

				int conversionResult;
				if (argument1.IsGenericParameter)
				{
					// generic parameter may be substituted with anything
					// including value types
					conversionResult = genericParameterDistance(argument1, argument2, exactly);
				}
				else if (argument2.IsGenericParameter)
				{
					conversionResult = genericParameterDistance(argument2, argument1, exactly);
				}
				else if (attributes.HasFlag(GenericParameterAttributes.Contravariant))
				{
					// generic variance applies to ref-types only
					if (argument1.IsValueType)
						return int.MaxValue;

					// dist(X<in T1>, X<in T2>) = dist(T2, T1)
					conversionResult = DistanceFrom(argument2, argument1, exactly);
				}
				else if (attributes.HasFlag(GenericParameterAttributes.Covariant))
				{
					if (argument2.IsValueType)
						return int.MaxValue;

					// dist(X<out T1>, X<out T2>) = dist(T1, T2)
					conversionResult = DistanceFrom(argument1, argument2, exactly);
				}
				else
				{
					// No possible conversion found.
					return int.MaxValue;
				}

				if (conversionResult == int.MaxValue)
					return int.MaxValue;

				checked
				{
					result += conversionResult;
				}
			}

			return result;
		}

		/// <summary>
		/// Checks if a type can be used as a substitute for a generic parameter.
		/// </summary>
		private int genericParameterDistance(Type varType, Type exprType, bool exactly = false)
		{
			// generic parameter is on the same level of inheritance as the expression
			// therefore getting its parent type does not take a step
			return varType.IsGenericParameter
				? DistanceFrom(varType.BaseType, exprType, exactly)
				: DistanceFrom(exprType.BaseType, varType, exactly);
		}

		/// <summary>
		/// Checks if a lambda signature matches a delegate.
		/// </summary>
		private int lambdaDistance(Type varType, Type exprType)
		{
			if (!varType.IsCallableType())
				return int.MaxValue;

			var varWrapper = WrapDelegate(varType);
			var exprWrapper = WrapDelegate(exprType);

			if (varWrapper.ArgumentTypes.Length != exprWrapper.ArgumentTypes.Length)
				return int.MaxValue;

			// return type is not checked until lambda argument types are substituted

			var sum = 0;
			for (var idx = 0; idx < varWrapper.ArgumentTypes.Length; idx++)
			{
				var currVar = varWrapper.ArgumentTypes[idx];
				var currExpr = exprWrapper.ArgumentTypes[idx];

				var dist = DistanceFrom(currVar, currExpr);
				if (dist == int.MaxValue)
					return int.MaxValue;

				sum += dist;
			}

			return sum;
		}

		#endregion

		#region Most common type

		/// <summary>
		/// Gets the most common type that all the given types would fit into.
		/// </summary>
		public Type GetMostCommonType(params Type[] types)
		{
			if (types.Length == 0)
				return null;

			if (types.Length == 1)
				return types[0];

			// try to get the most wide type
			Type curr = null;
			foreach (var type in types)
			{
				if (type.IsInterface)
				{
					curr = typeof(object);
					break;
				}

				curr = getMostCommonType(curr, type);
				if (curr == typeof(object))
					break;
			}

			// check for cases that are not transitively castable
			// for example: new [1; 1.2; null]
			// int -> double is fine, double -> Nullable<double> is fine as well
			// but int -> Nullable<double> is currently forbidden
			foreach (var type in types)
			{
				if (!IsExtendablyAssignableFrom(curr, type))
				{
					curr = typeof(object);
					break;
				}
			}

			if (!curr.IsAnyOf(typeof(object), typeof(ValueType), typeof(Delegate), typeof(Enum)))
				return curr;

			// try to get common interfaces
			var ifaces = types[0].GetInterfaces().AsEnumerable();
			for (var idx = 1; idx < types.Length; idx++)
			{
				ifaces = ifaces.Intersect(types[idx].IsInterface ? new[] { types[idx] } : types[idx].GetInterfaces());
				if (!ifaces.Any())
					break;
			}

			var iface = getMostSpecificInterface(ifaces);
			return iface ?? typeof(object);
		}

		/// <summary>
		/// Gets the most common type between two.
		/// </summary>
		private Type getMostCommonType(Type left, Type right)
		{
			// corner case
			if (left == null || left == right)
				return right;

			if (right.IsInterface)
				return typeof(object);

			// valuetype & null
			if (left == typeof(NullType) && right.IsValueType)
				return typeof(Nullable<>).MakeGenericType(right);

			if (right == typeof(NullType) && left.IsValueType)
				return typeof(Nullable<>).MakeGenericType(left);

			// valuetype & Nullable<valuetype>
			if (left.IsNullableType() && left.GetGenericArguments()[0] == right)
				return left;

			if (right.IsNullableType() && right.GetGenericArguments()[0] == left)
				return right;

			// numeric extensions
			if (left.IsNumericType() && right.IsNumericType())
				return GetNumericOperationType(left, right) ?? typeof(object);

			// arrays
			if (left.IsArray && right.IsArray)
			{
				var leftElem = left.GetElementType();
				var rightElem = right.GetElementType();
				return leftElem.IsValueType || rightElem.IsValueType
					? typeof(object)
					: getMostCommonType(leftElem, rightElem).MakeArrayType();
			}

			// inheritance
			var currLeft = left;
			while (currLeft != null)
			{
				var currRight = right;
				while (currRight != null)
				{
					if (currLeft == currRight)
						return currLeft;

					currRight = currRight.BaseType;
				}

				currLeft = currLeft.BaseType;
			}

			return typeof(object);
		}

		private static Type getMostSpecificInterface(IEnumerable<Type> ifaces)
		{
			var remaining = ifaces.ToDictionary(i => i, i => true);
			foreach (var iface in ifaces)
			{
				foreach (var curr in iface.GetInterfaces())
					remaining.Remove(curr);
			}

			if (remaining.Count == 1)
				return remaining.First().Key;

			var preferred = new[] { typeof(IList<>), typeof(IEnumerable<>), typeof(IList) };
			foreach (var pref in preferred)
			{
				foreach (var curr in remaining.Keys)
					if (curr == pref || (curr.IsGenericType && curr.GetGenericTypeDefinition() == pref))
						return curr;
			}

			return null;
		}

		#endregion

		#region Numeric type conversions

		/// <summary>
		/// Get the best numeric operation type for two operands.
		/// </summary>
		/// <param name="type1">First operand type.</param>
		/// <param name="type2">Second operand type.</param>
		/// <returns>Operation type. <c>null</c> if operation not permitted.</returns>
		public Type GetNumericOperationType(Type type1, Type type2)
		{
			if (type1.IsFloatType() || type2.IsFloatType())
			{
				if (type1 == typeof(long) || type2 == typeof(long))
					return typeof(double);

				return widestNumericType(TypeExtensions.FloatTypes, type1, type2);
			}

			if (type1.IsSignedIntegerType() && type2.IsSignedIntegerType())
			{
				var types = TypeExtensions.SignedIntegerTypes.SkipWhile(type => type != typeof(int)).ToArray();
				return widestNumericType(types, type1, type2);
			}

			if (type1.IsUnsignedIntegerType() && type2.IsUnsignedIntegerType())
			{
				var index1 = Array.IndexOf(TypeExtensions.UnsignedIntegerTypes, type1);
				var index2 = Array.IndexOf(TypeExtensions.UnsignedIntegerTypes, type2);
				var uintIndex = Array.IndexOf(TypeExtensions.UnsignedIntegerTypes, typeof(uint));
				if (index1 < uintIndex && index2 < uintIndex)
					return typeof(int);

				return widestNumericType(TypeExtensions.UnsignedIntegerTypes, type1, type2);
			}

			// type1.IsSignedIntegerType() && type2.IsUnsignedIntegerType() or vice versa:
			return null;
		}

		private static Type widestNumericType(Type[] types, Type type1, Type type2)
		{
			var index1 = Array.IndexOf(types, type1);
			var index2 = Array.IndexOf(types, type2);
			var index = Math.Max(index1, index2);
			return types[index < 0 ? 0 : index];
		}

		private static int numericTypeConversion(Type varType, Type exprType)
		{
			if (varType.IsSignedIntegerType() && exprType.IsSignedIntegerType())
				return simpleNumericConversion(varType, exprType, TypeExtensions.SignedIntegerTypes);

			if (varType.IsUnsignedIntegerType() && exprType.IsUnsignedIntegerType())
				return simpleNumericConversion(varType, exprType, TypeExtensions.UnsignedIntegerTypes);

			if (varType.IsFloatType() && exprType.IsFloatType())
				return simpleNumericConversion(varType, exprType, TypeExtensions.FloatTypes);

			if (varType.IsSignedIntegerType() && exprType.IsUnsignedIntegerType())
				return unsignedToSignedConversion(varType, exprType);

			if (varType.IsFloatType() && exprType.IsSignedIntegerType())
				return signedToFloatConversion(varType, exprType);

			if (varType.IsFloatType() && exprType.IsUnsignedIntegerType())
				return unsignedToFloatConversion(varType, exprType);

			return int.MaxValue;
		}

		private static int simpleNumericConversion(Type varType, Type exprType, Type[] conversionChain)
		{
			var varTypeIndex = Array.IndexOf(conversionChain, varType);
			var exprTypeIndex = Array.IndexOf(conversionChain, exprType);
			if (varTypeIndex < exprTypeIndex)
				return int.MaxValue;

			return varTypeIndex - exprTypeIndex;
		}

		private static int unsignedToSignedConversion(Type varType, Type exprType)
		{
			// no unsigned type can be converted to the signed byte.
			if (varType == typeof(sbyte))
				return int.MaxValue;

			var index = Array.IndexOf(TypeExtensions.SignedIntegerTypes, varType);
			var correspondingUnsignedType = TypeExtensions.UnsignedIntegerTypes[index - 1]; // only expanding conversions allowed

			var result = simpleNumericConversion(correspondingUnsignedType, exprType, TypeExtensions.UnsignedIntegerTypes);
			return result == int.MaxValue
				? int.MaxValue
				: result + 1;
		}

		private static int signedToFloatConversion(Type varType, Type exprType)
		{
			var targetType = getCorrespondingSignedType(varType);

			var result = simpleNumericConversion(targetType, exprType, TypeExtensions.SignedIntegerTypes);
			return result == int.MaxValue
				? int.MaxValue
				: result + 1;
		}

		private static int unsignedToFloatConversion(Type varType, Type exprType)
		{
			if (exprType == typeof(ulong) && varType == typeof(decimal))
			{
				// ulong can be implicitly converted only to decimal.
				return 1;
			}
			else
			{
				// If type is not ulong we need to convert it to the corresponding signed type.
				var correspondingSignedType = getCorrespondingSignedType(varType);
				var result = unsignedToSignedConversion(correspondingSignedType, exprType);

				return result == int.MaxValue
					? int.MaxValue
					: result + 1;
			}
		}

		private static Type getCorrespondingSignedType(Type floatType)
		{
			if (floatType == typeof(float))
				return typeof(int);

			if (floatType == typeof(double) || floatType == typeof(decimal))
				return typeof(long);

			return null;
		}

		#endregion

		#region Type list distance

		/// <summary>
		/// Gets total distance between two sets of argument types.
		/// </summary>
		public MethodLookupResult<T> ArgumentDistance<T>(IEnumerable<Type> passedTypes, Type[] actualTypes, T method, bool isVariadic)
		{
			if (!isVariadic)
				return new MethodLookupResult<T>(method, TypeListDistance(passedTypes, actualTypes), actualTypes);

			var simpleCount = actualTypes.Length - 1;

			var simpleDistance = TypeListDistance(passedTypes.Take(simpleCount), actualTypes.Take(simpleCount));
			var variadicDistance = variadicArgumentDistance(passedTypes.Skip(simpleCount), actualTypes[simpleCount]);
			var distance = simpleDistance == int.MaxValue || variadicDistance == int.MaxValue ? int.MaxValue : simpleDistance + variadicDistance;
			return new MethodLookupResult<T>(method, distance, actualTypes);
		}

		/// <summary>
		/// Gets total distance between two sequence of types.
		/// </summary>
		public int TypeListDistance(IEnumerable<Type> passedArgs, IEnumerable<Type> calleeArgs)
		{
			var passedIter = passedArgs.GetEnumerator();
			var calleeIter = calleeArgs.GetEnumerator();

			var totalDist = 0;
			while (true)
			{
				var passedOk = passedIter.MoveNext();
				var calleeOk = calleeIter.MoveNext();

				// argument count differs: method cannot be applied
				if (passedOk != calleeOk)
					return int.MaxValue;

				// both sequences have finished
				if (!calleeOk)
					return totalDist;

				var dist = DistanceFrom(calleeIter.Current, passedIter.Current);
				if (dist == int.MaxValue)
					return int.MaxValue;

				totalDist += dist;
			}
		}

		/// <summary>
		/// Calculates the compound distance of a list of arguments packed into a param array.
		/// </summary>
		private int variadicArgumentDistance(IEnumerable<Type> passedArgs, Type variadicArg)
		{
			var args = passedArgs.ToArray();

			// variadic function invoked with an array: no conversion
			if (args.Length == 1 && args[0] == variadicArg)
				return 0;

			var sum = 0;
			var elemType = variadicArg.GetElementType();

			foreach (var curr in args)
			{
				var currDist = DistanceFrom(elemType, curr);
				if (currDist == int.MaxValue)
					return int.MaxValue;

				sum += currDist;
			}

			// 1 extra distance point for packing arguments into the array:
			// otherwise fun(int) and fun(int, object[]) will have equal distance for `fun 1` and cause an ambiguity error
			return sum + 1;
		}

		#endregion

		#region Interface implementations

		/// <summary>
		/// Checks if a type implements an interface.
		/// </summary>
		/// <param name="type">Type to check.</param>
		/// <param name="iface">Desired interface.</param>
		/// <param name="unwindGenerics">A flag indicating that generic arguments should be discarded from both the type and the interface.</param>
		public bool Implements(Type type, Type iface, bool unwindGenerics)
		{
			var ifaces = ResolveInterfaces(type);
			if (type.IsInterface)
				ifaces = ifaces.Union(new[] { type }).ToArray();

			if (unwindGenerics)
			{
				for (var idx = 0; idx < ifaces.Length; idx++)
				{
					var curr = ifaces[idx];
					if (curr.IsGenericType)
						ifaces[idx] = curr.GetGenericTypeDefinition();
				}

				if (iface.IsGenericType)
					iface = iface.GetGenericTypeDefinition();
			}

			return ifaces.Contains(iface);
		}

		/// <summary>
		/// Finds an implementation of a generic interface.
		/// </summary>
		/// <param name="type">Type to find the implementation in.</param>
		/// <param name="iface">Desirrable interface.</param>
		/// <returns>Implementation of the generic interface or null if none.</returns>
		public Type ResolveImplementationOf(Type type, Type iface)
		{
			if (iface.IsGenericType && !iface.IsGenericTypeDefinition)
				iface = iface.GetGenericTypeDefinition();

			var ifaces = ResolveInterfaces(type);
			if (type.IsInterface)
				ifaces = ifaces.Union(new[] { type }).ToArray();

			return ifaces.FirstOrDefault(
				x => x == iface || (x.IsGenericType && x.GetGenericTypeDefinition() == iface)
			);
		}

		/// <summary>
		/// Resolves the common implementation of the given interface for two types.
		/// </summary>
		/// <param name="iface">Interface to find an implementation for in given types.</param>
		/// <param name="type1">First type to examine.</param>
		/// <param name="type2">First type to examine.</param>
		/// <returns>Common implementation of an interface, or null if none.</returns>
		public Type ResolveCommonImplementationFor(Type iface, Type type1, Type type2)
		{
			var impl1 = ResolveImplementationOf(type1, iface);
			var impl2 = ResolveImplementationOf(type2, iface);
			return impl1 == impl2 ? impl1 : null;
		}

		/// <summary>
		/// Checks if a type is (or implements) a specified type with any generic argument values given.
		/// Example: Dictionary&lt;A, B&gt; is Dictionary`2
		/// </summary>
		/// <param name="type">Closed type to test.</param>
		/// <param name="genericType">Generic type.</param>
		public bool IsAppliedVersionOf(Type type, Type genericType)
		{
			if (type.IsInterface && !genericType.IsInterface)
				throw new ArgumentException(string.Format("Interface {0} cannot implement a type! ({1} given).", type.FullName, genericType.FullName));

			if (!type.IsGenericType || !genericType.IsGenericType)
				return false;

			return genericType.IsInterface
				? Implements(type, genericType, true)
				: type.GetGenericTypeDefinition() == genericType.GetGenericTypeDefinition();
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Returns the list of methods by name, flattening interface hierarchy.
		/// </summary>
		public IEnumerable<MethodInfo> getMethodsByName(Type type, string name)
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

			var result = type.GetMethods(flags).Where(m => m.Name == name).ToArray();
			if (type.IsInterface && !result.Any())
				result = type.GetInterfaces()
							 .Select(x => x.IsGenericType ? x.GetGenericTypeDefinition() : x)
							 .SelectMany(x => getMethodsByName(x, name))
							 .ToArray();

			return result;
		}

		/// <summary>
		/// Resolves an "actual" declaring type if generic workaround has been applied to an interface.
		/// </summary>
		/// <param name="type">Actual type</param>
		/// <param name="decl">Declaring generic type of method or property</param>
		private Type resolveActualDeclaringType(Type type, Type decl)
		{
			if (type.IsInterface && type != decl)
			{
				var ifaces = ResolveInterfaces(type);
				foreach (var curr in ifaces)
				{
					if (curr == decl || (curr.IsGenericType && decl.IsGenericType && curr.GetGenericTypeDefinition() == decl.GetGenericTypeDefinition()))
						return curr;
				}
			}

			return type;
		}

		/// <summary>
		/// Creates a generic method version for a specific type.
		/// </summary>
		private MethodInfo getMethodVersionForType(Type type, MethodInfo method)
		{
			if (method != null && type.IsGenericType)
				return TypeBuilder.GetMethod(type, method);

			return method;
		}

		/// <summary>
		/// Throws a new error.
		/// </summary>
		[ContractAnnotation("=> halt")]
		[DebuggerStepThrough]
		private static void error(string msg, params object[] args)
		{
			throw new LensCompilerException(string.Format(msg, args));
		}

		#endregion
	}

	public class TypeMatchException : Exception
	{
		public TypeMatchException() { }
		public TypeMatchException(string msg) : base(msg) { }
	}

	/// <summary>
	/// Callback type for lambda resolution.
	/// </summary>
	internal delegate Type LambdaResolver(int lambdaPosition, Type[] argTypes);
}
