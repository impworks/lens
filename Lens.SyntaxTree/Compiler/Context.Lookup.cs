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
			try
			{
				var field = type.GetField(name);
				return new FieldWrapper(field, type, field.FieldType);
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var genField = genType.GetField(name);

				return new FieldWrapper(
					TypeBuilder.GetField(type, genField),
					type,
					GenericHelper.ApplyGenericArguments(genField.FieldType, type)
				);
			}
		}

//		/// <summary>
//		/// Resolves a property 
//		/// </summary>
//		public PropertyWrapper ResolveProperty(Type type, string name)
//		{
//			
//		}

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
		public static Tuple<T, int> ResolveMethodByArgs<T>(IEnumerable<T> list, Func<T, Type[]> argsGetter, Type[] args)
		{
			Func<T, Tuple<T, int>> methodEvaluator = ent => new Tuple<T, int>(ent, ExtensionMethodResolver.GetArgumentsDistance(args, argsGetter(ent)));

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
