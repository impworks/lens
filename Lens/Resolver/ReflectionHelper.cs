using System;
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
	/// A collection is useful tools for working with Reflection entities.
	/// </summary>
	internal static class ReflectionHelper
	{
		#region Various resolvers

		/// <summary>
		/// Resolves a field from a type by its name.
		/// </summary>
		public static FieldWrapper ResolveField(Type type, string name)
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
					FieldType = GenericHelper.ApplyGenericArguments(genField.FieldType, type)
				};
			}
		}

		/// <summary>
		/// Resolves a property from a type by its name.
		/// </summary>
		public static PropertyWrapper ResolveProperty(Type type, string name)
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
					PropertyType = GenericHelper.ApplyGenericArguments(genPty.PropertyType, type),
				};
			}
		}

		/// <summary>
		/// Resolves a constructor from a type by the list of arguments.
		/// </summary>
		public static ConstructorWrapper ResolveConstructor(Type type, Type[] argTypes)
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
					c => c.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
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
		/// Resolves an event by its name.
		/// </summary>
		public static EventWrapper ResolveEvent(Type type, string name)
		{
			try
			{
				var evt = type.GetEvent(name);
				if (evt == null)
					throw new KeyNotFoundException();

				return new EventWrapper
				{
					Name = name,
					Type = type,

					IsStatic = evt.GetRemoveMethod().IsStatic,

					AddMethod = evt.GetAddMethod(),
					RemoveMethod = evt.GetRemoveMethod(),
					EventHandlerType = evt.EventHandlerType
				};
			}
			catch (NotSupportedException)
			{
				if (!type.IsGenericType)
					throw new KeyNotFoundException();

				var genType = type.GetGenericTypeDefinition();
				var genEvt = genType.GetEvent(name);

				if (genEvt == null)
					throw new KeyNotFoundException();

				var adder = genEvt.GetAddMethod();
				var remover = genEvt.GetRemoveMethod();

				var declType = resolveActualDeclaringType(type, genEvt.DeclaringType);

				return new EventWrapper
				{
					Name = name,
					Type = type,

					AddMethod = getMethodVersionForType(declType, adder),
					RemoveMethod = getMethodVersionForType(declType, remover),
					IsStatic = adder.IsStatic,
					EventHandlerType = GenericHelper.ApplyGenericArguments(genEvt.EventHandlerType, type)
				};
			}
		}

		/// <summary>
		/// Resolves a method by its name and argument types. If generic arguments are passed, they are also applied.
		/// Generic arguments whose values can be inferred from argument types can be skipped.
		/// </summary>
		public static MethodWrapper ResolveMethod(Type type, string name, Type[] argTypes, Type[] hints, LambdaResolver lambdaResolver)
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
					var genericValues = GenericHelper.ResolveMethodGenericsByArgs(method.ArgumentTypes, argTypes, genericDefs, hints);

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
					m => m.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type, false)).ToArray(),
					IsVariadic,
					argTypes
				);

				var mInfoOriginal = genMethod.Method;
				var declType = resolveActualDeclaringType(type, mInfoOriginal.DeclaringType);
				var mInfo = getMethodVersionForType(declType, mInfoOriginal);

				if (mInfoOriginal.IsGenericMethod)
				{
					var genericDefs = mInfoOriginal.GetGenericArguments();
					var genericValues = GenericHelper.ResolveMethodGenericsByArgs(genMethod.ArgumentTypes, argTypes, genericDefs, hints, lambdaResolver);

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
						error(CompilerMessages.GenericArgsToNonGenericMethod, name);

					mw.ArgumentTypes = mInfoOriginal.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray();
					mw.ReturnType = GenericHelper.ApplyGenericArguments(mInfoOriginal.ReturnType, type, false);
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
		public static MethodWrapper ResolveExtensionMethod(ExtensionMethodResolver resolver, Type type, string name, Type[] argTypes, Type[] hints, LambdaResolver lambdaResolver)
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

				var genericValues = GenericHelper.ResolveMethodGenericsByArgs(
					expectedTypes,
					extMethodArgs.ToArray(),
					genericDefs,
					hints,
					lambdaResolver
				);

				info.GenericArguments = genericValues;
				info.MethodInfo = info.MethodInfo.MakeGenericMethod(genericValues);
				info.ReturnType = GenericHelper.ApplyGenericArguments(info.ReturnType, genericDefs, genericValues);
				info.ArgumentTypes = expectedTypes.Select(t => GenericHelper.ApplyGenericArguments(t, genericDefs, genericValues)).ToArray();
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
		public static IEnumerable<MethodWrapper> ResolveMethodGroup(Type type, string name)
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
							ArgumentTypes = m.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, declType)).ToArray(),
							ReturnType = GenericHelper.ApplyGenericArguments(m.ReturnType, declType)
						};
					}
				);
			}
		}


		/// <summary>
		/// Resolves an indexer property from a type by its argument.
		/// </summary>
		public static MethodWrapper ResolveIndexer(Type type, Type idxType, bool isGetter)
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
				var indexer = resolveIndexerProperty(genType, idxType, isGetter, p => GenericHelper.ApplyGenericArguments(p, type));
				var declType = resolveActualDeclaringType(type, indexer.DeclaringType);

				return new MethodWrapper
				{
					Type = type,

					MethodInfo = getMethodVersionForType(declType, indexer),
					IsStatic = false,
					IsVirtual = indexer.IsVirtual,
					ArgumentTypes = indexer.GetParameters().Select(p => GenericHelper.ApplyGenericArguments(p.ParameterType, type)).ToArray(),
					ReturnType = GenericHelper.ApplyGenericArguments(indexer.ReturnType, type)
				};
			}
		}

		/// <summary>
		/// Finds a property that can work as an index.
		/// </summary>
		private static MethodInfo resolveIndexerProperty(Type type, Type idxType, bool isGetter, Func<Type, Type> typeProcessor)
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
		public static MethodLookupResult<T> ResolveMethodByArgs<T>(IEnumerable<T> list, Func<T, Type[]> argsGetter, Func<T, bool> isVariadicGetter, Type[] argTypes)
		{
			var result = list.Select(x => TypeExtensions.ArgumentDistance(argTypes, argsGetter(x), x, isVariadicGetter(x)))
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

		private static readonly Dictionary<Type, Type[]> m_InterfaceCache = new Dictionary<Type, Type[]>();

		/// <summary>
		/// Get interfaces of a possibly generic type.
		/// </summary>
		public static Type[] ResolveInterfaces(this Type type)
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
							ifaces[idx] = GenericHelper.ApplyGenericArguments(curr, type);
					}
				}

				else if (type.IsArray)
				{
					// replace interfaces of any array with element type
					var elem = type.GetElementType();
					ifaces = typeof (int[]).GetInterfaces();
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
		public static MethodWrapper WrapDelegate(Type type)
		{
			if (!type.IsCallableType())
				throw new ArgumentException("type");

			return ResolveMethodGroup(type, "Invoke").Single();
		}

		/// <summary>
		/// Checks if two delegates can be combined.
		/// </summary>
		public static bool CanCombineDelegates(Type left, Type right)
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
		public static Type CombineDelegates(Type left, Type right)
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
		public static bool IsVariadic(MethodBase method)
		{
			var args = method.GetParameters();
			return args.Length > 0 && args[args.Length - 1].IsDefined(typeof(ParamArrayAttribute), true);
		}

		/// <summary>
		/// Checks if the list of argument types denotes a partial application case.
		/// </summary>
		public static bool IsPartiallyApplied(Type[] argTypes)
		{
			return argTypes.Contains(typeof(UnspecifiedType));
		}

		/// <summary>
		/// Checks if the list of argument types contains a lambda expression with unresolved argument types.
		/// </summary>
		public static bool IsPartiallyResolved(Type[] argTypes)
		{
			for(var idx = 0; idx < argTypes.Length; idx++)
				if (argTypes[idx].IsLambdaType())
					return true;

			return false;
		}

		/// <summary>
		/// Checks if the possibly generic type has a default constructor.
		/// </summary>
		public static bool HasDefaultConstructor(this Type type)
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
					return type.GetGenericTypeDefinition().HasDefaultConstructor();

				// arrays do not have constructors
				if (type.IsArray)
					return false;

				// type labels and records have constructors
				return true;
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Returns the list of methods by name, flattening interface hierarchy.
		/// </summary>
		private static IEnumerable<MethodInfo> getMethodsByName(Type type, string name)
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
		private static Type resolveActualDeclaringType(Type type, Type decl)
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
		private static MethodInfo getMethodVersionForType(Type type, MethodInfo method)
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
}
