using System;
using System.Collections.Generic;
using Lens.Compiler;

namespace Lens.Resolver
{
	/// <summary>
	/// A type that provides helpers for manipulating Func and Action types.
	/// </summary>
	internal static class FunctionalHelper
	{
		#region Static constructor

		static FunctionalHelper()
		{
			_ActionBaseTypes = new[]
			{
				typeof(Action<>),
				typeof(Action<,>),
				typeof(Action<,,>),
				typeof(Action<,,,>),
				typeof(Action<,,,,>),
				typeof(Action<,,,,,>),
				typeof(Action<,,,,,,>),
				typeof(Action<,,,,,,,>),
				typeof(Action<,,,,,,,,>),
				typeof(Action<,,,,,,,,,>),
				typeof(Action<,,,,,,,,,,>),
				typeof(Action<,,,,,,,,,,,>),
				typeof(Action<,,,,,,,,,,,,>),
				typeof(Action<,,,,,,,,,,,,,>),
				typeof(Action<,,,,,,,,,,,,,,>),
				typeof(Action<,,,,,,,,,,,,,,,>)
			};

			_FuncBaseTypes = new[]
			{
				typeof(Func<>),
				typeof(Func<,>),
				typeof(Func<,,>),
				typeof(Func<,,,>),
				typeof(Func<,,,,>),
				typeof(Func<,,,,,>),
				typeof(Func<,,,,,,>),
				typeof(Func<,,,,,,,>),
				typeof(Func<,,,,,,,,>),
				typeof(Func<,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,,,,,,>),
				typeof(Func<,,,,,,,,,,,,,,,,>),
			};

			_LambdaBaseTypes = new[]
			{
				typeof(Lambda<>),
				typeof(Lambda<,>),
				typeof(Lambda<,,>),
				typeof(Lambda<,,,>),
				typeof(Lambda<,,,,>),
				typeof(Lambda<,,,,,>),
				typeof(Lambda<,,,,,,>),
				typeof(Lambda<,,,,,,,>),
				typeof(Lambda<,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,,,,,,>),
				typeof(Lambda<,,,,,,,,,,,,,,,>)
			};

			_TupleBaseTypes = new[]
			{
				typeof(Tuple<>),
				typeof(Tuple<,>),
				typeof(Tuple<,,>),
				typeof(Tuple<,,,>),
				typeof(Tuple<,,,,>),
				typeof(Tuple<,,,,,>),
				typeof(Tuple<,,,,,,>),
				typeof(Tuple<,,,,,,,>),
			};

			_ActionTypesLookup = new HashSet<Type>(_ActionBaseTypes);
			_FuncTypesLookup = new HashSet<Type>(_FuncBaseTypes);
			_LambdaTypesLookup = new HashSet<Type>(_LambdaBaseTypes);
			_TupleTypesLookup = new HashSet<Type>(_TupleBaseTypes);
		}

		#endregion

		#region Fields

		private static readonly Type[] _ActionBaseTypes;
		private static readonly Type[] _FuncBaseTypes;
		private static readonly Type[] _LambdaBaseTypes;
		private static readonly Type[] _TupleBaseTypes;

		private static readonly HashSet<Type> _ActionTypesLookup;
		private static readonly HashSet<Type> _FuncTypesLookup;
		private static readonly HashSet<Type> _LambdaTypesLookup;
		private static readonly HashSet<Type> _TupleTypesLookup;

		#endregion

		#region Type kind methods

		/// <summary>
		/// Checks if a type is a function type.
		/// </summary>
		public static bool IsFuncType(this Type type)
		{
			return isKnownType(_FuncTypesLookup, type);
		}

		/// <summary>
		/// Checks if a type is an action type;
		/// </summary>
		public static bool IsActionType(this Type type)
		{
			return type == typeof(Action) || isKnownType(_ActionTypesLookup, type);
		}

		/// <summary>
		/// Checks if a type is a function type.
		/// </summary>
		public static bool IsLambdaType(this Type type)
		{
			return isKnownType(_LambdaTypesLookup, type);
		}

		/// <summary>
		/// Checks if a type is a tuple type.
		/// </summary>
		public static bool IsTupleType(this Type type)
		{
			return isKnownType(_TupleTypesLookup, type);
		}

		/// <summary>
		/// Checks if the type can be called.
		/// </summary>
		public static bool IsCallableType(this Type type)
		{
			while (type != null)
			{
				if (type == typeof(MulticastDelegate))
					return true;
				type = type.BaseType;
			}

			return false;
		}

		#endregion

		#region Type constructing

		/// <summary>
		/// Creates a Func or Action depending on return type.
		/// </summary>
		public static Type CreateDelegateType(Type returnType, params Type[] args)
		{
			return returnType.IsVoid()
				? CreateActionType(args)
				: CreateFuncType(returnType, args);
		}

		/// <summary>
		///	Creates a new function type with argument types applied.
		/// </summary>
		public static Type CreateFuncType(Type returnType, params Type[] args)
		{
			if(args.Length > 16)
				throw new LensCompilerException("Func<> can have up to 16 arguments!");

			var baseType = _FuncBaseTypes[args.Length];
			var argTypes = new List<Type>(args) {returnType};
			return baseType.MakeGenericType(argTypes.ToArray());
		}

		/// <summary>
		///	Creates a new function type with argument types applied.
		/// </summary>
		public static Type CreateActionType(params Type[] args)
		{
			if (args.Length > 16)
				throw new LensCompilerException("Action<> can have up to 16 arguments!");

			if (args.Length == 0)
				return typeof (Action);

			var baseType = _ActionBaseTypes[args.Length-1];
			return baseType.MakeGenericType(args);
		}

		/// <summary>
		///	Creates a new function type with argument types applied.
		/// </summary>
		public static Type CreateLambdaType(params Type[] args)
		{
			if (args.Length > 16)
				throw new LensCompilerException("Lambda<> can have up to 16 arguments!");

			// sic!
			// no need for a special parameterless lambda
			if (args.Length == 0)
				return typeof(Func<UnspecifiedType>);

			var baseType = _LambdaBaseTypes[args.Length - 1];
			return baseType.MakeGenericType(args);
		}

		/// <summary>
		/// Creates a new tuple type with given argument types.
		/// </summary>
		public static Type CreateTupleType(params Type[] args)
		{
			if(args.Length > 8)
				throw new LensCompilerException("Tuple<> can have up to 8 type arguments!");

			var baseType = _TupleBaseTypes[args.Length - 1];
			return baseType.MakeGenericType(args);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Checks if a type is generic and is contained in the lookup table.
		/// </summary>
		private static bool isKnownType(HashSet<Type> typesLookup, Type type)
		{
			return type.IsGenericType && typesLookup.Contains(type.GetGenericTypeDefinition());
		}

		#endregion
	}
}
