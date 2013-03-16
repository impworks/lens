using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A type that provides helpers for manipulating Func and Action types.
	/// </summary>
	public static class FunctionalHelper
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
		/// Gets the argument types of a delegate.
		/// </summary>
		public static Type[] GetArgumentTypes(this Delegate method)
		{
			return method.GetType().GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
		}

		/// <summary>
		/// Gets the argument types of a function or action type.
		/// </summary>
		public static Type[] GetArgumentTypes(this Type type)
		{
			if(!type.IsCallableType())
				throw new LensCompilerException(string.Format("Type '{0}' is not callable!", type.Name));

			return type.GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
		}

		/// <summary>
		/// Gets the return type of a function.
		/// </summary>
		public static Type GetReturnType(this Delegate del)
		{
			return del.GetType().GetMethod("Invoke").ReturnType;
		}

		/// <summary>
		/// Gets the return type of a function.
		/// </summary>
		public static Type GetReturnType(this Type type)
		{
			if (type.IsActionType())
				return typeof (void);

			if(!type.IsFuncType())
				throw new LensCompilerException(string.Format("Type '{0}' is not a callable type!", type.Name));

			var args = type.GetGenericArguments();
			return args[args.Length - 1];
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
		/// Creates a new tuple type with given argument types.
		/// </summary>
		public static Type CreateTupleType(params Type[] args)
		{
			// todo: infinitive tuples using TRest
			if(args.Length > 8)
				throw new LensCompilerException("Tuple<> can have up to 8 type arguments!");

			var baseType = _TupleBaseTypes[args.Length - 1];
			return baseType.MakeGenericType(args);
		}

		/// <summary>
		/// Creates a curry-friendly version of the function.
		/// </summary>
		public static Type DiscardParameters(this Type type, int count)
		{
			if (type.IsActionType())
			{
				var argCount = type == typeof (Action) ? 0 : Array.IndexOf(_ActionBaseTypes, type.GetGenericTypeDefinition()) + 1;
				var newCount = argCount - count;

				if(newCount < 0)
					throw new LensCompilerException(string.Format("Cannot discard more than {0} parameters from type '{1}'!", argCount, type));

				if(newCount == 0)
					return typeof (Action);

				var newTypes = type.GetGenericArguments().Skip(count).ToArray();
				return _ActionBaseTypes[newCount - 1].MakeGenericType(newTypes);
			}

			if (type.IsFuncType())
			{
				var argCount = Array.IndexOf(_FuncBaseTypes, type.GetGenericTypeDefinition());
				var newCount = argCount - count;

				if(newCount < 0)
					throw new LensCompilerException(string.Format("Cannot discard more than {0} parameters from type '{1}'!", argCount, type));

				var newTypes = type.GetGenericArguments().Skip(count).ToArray();
				return _FuncBaseTypes[newCount].MakeGenericType(newTypes);
			}

			throw new LensCompilerException(string.Format("Type '{0}' is not callable!", type.Name));
		}

		/// <summary>
		/// Creates a more generalized version of current Func or Action with prepended parameters.
		/// </summary>
		public static Type AddParameters(this Type type, params Type[] ps)
		{
			if (type.IsActionType())
			{
				var argCount = type == typeof(Action) ? 0 : Array.IndexOf(_ActionBaseTypes, type.GetGenericTypeDefinition()) + 1;
				var newCount = argCount + ps.Length;

				if (ps.Length == 0)
					return type;

				if(newCount > 16)
					throw new LensCompilerException("An Action cannot have more than 16 arguments!");

				var newTypes = new List<Type>(newCount);
				newTypes.AddRange(ps);
				newTypes.AddRange(type.GetGenericArguments());
				return _ActionBaseTypes[newCount - 1].MakeGenericType(newTypes.ToArray());
			}

			if (type.IsFuncType())
			{
				var argCount = Array.IndexOf(_FuncBaseTypes, type.GetGenericTypeDefinition());
				var newCount = argCount + ps.Length;

				if (ps.Length == 0)
					return type;

				if (newCount > 16)
					throw new LensCompilerException("A Func cannot have more than 16 arguments!");

				var newTypes = new List<Type>(newCount);
				newTypes.AddRange(ps);
				newTypes.AddRange(type.GetGenericArguments());
				return _FuncBaseTypes[newCount].MakeGenericType(newTypes.ToArray());
			}

			throw new LensCompilerException(string.Format("Type '{0}' is not callable!", type.Name));
		}
	}
}
