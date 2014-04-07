using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;

namespace Lens.Utils
{
	/// <summary>
	/// A type that provides helpers for manipulating Func and Action types.
	/// </summary>
	internal static class FunctionalHelper
	{
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
		}

		private static readonly Type[] _ActionBaseTypes;
		private static readonly Type[] _FuncBaseTypes;
		private static readonly Type[] _LambdaBaseTypes;
		private static readonly Type[] _TupleBaseTypes;

		/// <summary>
		/// Checks if a type is a function type.
		/// </summary>
		public static bool IsFuncType(this Type type)
		{
			return type.IsGenericType && _FuncBaseTypes.Contains(type.GetGenericTypeDefinition());
		}

		/// <summary>
		/// Checks if a type is an action type;
		/// </summary>
		public static bool IsActionType(this Type type)
		{
			if (type == typeof (Action))
				return true;

			return type.IsGenericType && _ActionBaseTypes.Contains(type.GetGenericTypeDefinition());
		}

		/// <summary>
		/// Checks if a type is a tuple type.
		/// </summary>
		public static bool IsTupleType(this Type type)
		{
			return type.IsGenericType && _TupleBaseTypes.Contains(type.GetGenericTypeDefinition());
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
	}
}
