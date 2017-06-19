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
			ActionBaseTypes = new[]
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

			FuncBaseTypes = new[]
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

			LambdaBaseTypes = new[]
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

			TupleBaseTypes = new[]
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

			ActionTypesLookup = new HashSet<Type>(ActionBaseTypes);
			FuncTypesLookup = new HashSet<Type>(FuncBaseTypes);
			LambdaTypesLookup = new HashSet<Type>(LambdaBaseTypes);
			TupleTypesLookup = new HashSet<Type>(TupleBaseTypes);
		}

		#endregion

		#region Fields

		private static readonly Type[] ActionBaseTypes;
		private static readonly Type[] FuncBaseTypes;
		private static readonly Type[] LambdaBaseTypes;
		private static readonly Type[] TupleBaseTypes;

		private static readonly HashSet<Type> ActionTypesLookup;
		private static readonly HashSet<Type> FuncTypesLookup;
		private static readonly HashSet<Type> LambdaTypesLookup;
		private static readonly HashSet<Type> TupleTypesLookup;

		#endregion

		#region Type kind methods

		/// <summary>
		/// Checks if a type is a function type.
		/// </summary>
		public static bool IsFuncType(this Type type)
		{
			return IsKnownType(FuncTypesLookup, type);
		}

		/// <summary>
		/// Checks if a type is an action type;
		/// </summary>
		public static bool IsActionType(this Type type)
		{
			return type == typeof(Action) || IsKnownType(ActionTypesLookup, type);
		}

		/// <summary>
		/// Checks if a type is a function type.
		/// </summary>
		public static bool IsLambdaType(this Type type)
		{
			return IsKnownType(LambdaTypesLookup, type);
		}

		/// <summary>
		/// Checks if a type is a tuple type.
		/// </summary>
		public static bool IsTupleType(this Type type)
		{
			return IsKnownType(TupleTypesLookup, type);
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

			var baseType = FuncBaseTypes[args.Length];
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

			var baseType = ActionBaseTypes[args.Length-1];
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

			var baseType = LambdaBaseTypes[args.Length - 1];
			return baseType.MakeGenericType(args);
		}

		/// <summary>
		/// Creates a new tuple type with given argument types.
		/// </summary>
		public static Type CreateTupleType(params Type[] args)
		{
			if(args.Length > 8)
				throw new LensCompilerException("Tuple<> can have up to 8 type arguments!");

			var baseType = TupleBaseTypes[args.Length - 1];
			return baseType.MakeGenericType(args);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Checks if a type is generic and is contained in the lookup table.
		/// </summary>
		private static bool IsKnownType(HashSet<Type> typesLookup, Type type)
		{
			return type.IsGenericType && typesLookup.Contains(type.GetGenericTypeDefinition());
		}

		#endregion
	}
}
