using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
	internal partial class TypeEntity
	{
		#region Auto-generated entities

		private void createSpecificEquals()
		{
			var eq = CreateMethod("Equals", "bool", new[] { Expr.Arg("other", Name) });

			// var result = true
			eq.Body.Add(Expr.Var("result", Expr.True()));

			foreach (var f in _Fields.Values)
			{
				var left = Expr.GetMember(Expr.This(), f.Name);
				var right = Expr.GetMember(Expr.Get("other"), f.Name);

				var isSeq = f.Type.IsGenericType && f.Type.Implements(typeof(IEnumerable<>), true);
				var expr = isSeq
					? Expr.Invoke("Enumerable", "SequenceEqual", left, right)
					: Expr.Invoke(Expr.This(), "Equals", Expr.Cast(left, "object"), Expr.Cast(right, "object"));

				eq.Body.Add(
					Expr.Set(
						"result",
						Expr.And(Expr.Get("result"), expr)
					)
				);
			}

			eq.Body.Add(Expr.Get("result"));
		}

		private void createGenericEquals()
		{
			var eq = CreateMethod(
				"Equals",
				"bool",
				new[] { Expr.Arg<object>("obj") },
				false,
				true
			);

			// if(this.ReferenceEquals null obj)
			//    false
			// else
			//    (this.ReferenceEquals this obj) || ( (obj.GetType () == this.GetType()) && (this.Equals obj as <Name>))

			eq.Body.Add(
				Expr.If(
					Expr.Invoke(Expr.This(), "ReferenceEquals", Expr.Null(), Expr.Get("obj")),
					Expr.Block(Expr.False()),
					Expr.Block(
						Expr.Or(
							Expr.Invoke(Expr.This(), "ReferenceEquals", Expr.This(), Expr.Get("obj")),
							Expr.And(
								Expr.Equal(
									Expr.Invoke(Expr.Get("obj"), "GetType"),
									Expr.Invoke(Expr.This(), "GetType")
								),
								Expr.Invoke(
									Expr.This(),
									"Equals",
									Expr.Cast(Expr.Get("obj"), Name)
								)
							)
						)
					)
				)
			);
		}

		private void createGetHashCode()
		{
			var ghc = CreateMethod(
				"GetHashCode",
				typeof(int),
				Type.EmptyTypes,
				false,
				true
			);

			// var result = 0
			ghc.Body.Add(Expr.Var("result", Expr.Int(0)));

			// result ^= (<field> != null ? field.GetHashCode() : 0) * 397
			var id = 0;
			foreach (var f in _Fields.Values)
			{
				var fieldType = f.Type ?? Context.ResolveType(f.TypeSignature);
				NodeBase expr;
				if (fieldType.IsIntegerType())
					expr = Expr.GetMember(Expr.This(), f.Name);
				else if (fieldType.IsValueType)
					expr = Expr.Invoke(
						Expr.Cast(Expr.GetMember(Expr.This(), f.Name), typeof(object)),
						"GetHashCode"
					);
				else
					expr = Expr.If(
						Expr.NotEqual(
							Expr.GetMember(Expr.This(), f.Name),
							Expr.Null()
						),
						Expr.Block(
							Expr.Invoke(
								Expr.GetMember(Expr.This(), f.Name),
								"GetHashCode"
							)
						),
						Expr.Block(Expr.Int(0))
					);

				if (id < _Fields.Count - 1)
					expr = Expr.Mult(expr, Expr.Int(397));

				ghc.Body.Add(
					Expr.Set("result", Expr.Xor(Expr.Get("result"), expr))
				);

				id++;
			}

			ghc.Body.Add(Expr.Get("result"));
		}

		private void createPureWrapper(MethodEntity method)
		{
			if (method.ReturnType.IsVoid())
				Context.Error(CompilerMessages.PureFunctionReturnUnit, method.Name);

			var pureName = string.Format(EntityNames.PureMethodNameTemplate, method.Name);
			var pure = CreateMethod(pureName, method.ReturnTypeSignature, method.Arguments.Values, true);
			pure.Body = method.Body;
			method.Body = null;

			var argCount = method.Arguments != null ? method.Arguments.Count : method.ArgumentTypes.Length;

			if (argCount >= 8)
				Context.Error(CompilerMessages.PureFunctionTooManyArgs, method.Name);

			if (argCount == 0)
				createPureWrapper0(method, pureName);
			else if (argCount == 1)
				createPureWrapper1(method, pureName);
			else
				createPureWrapperMany(method, pureName);
		}

		private void createPureWrapper0(MethodEntity wrapper, string originalName)
		{
			var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
			var flagName = string.Format(EntityNames.PureMethodCacheFlagNameTemplate, wrapper.Name);

			CreateField(fieldName, wrapper.ReturnTypeSignature, true);
			CreateField(flagName, typeof(bool), true);

			wrapper.Body = Expr.Block(

				// if (not $flag) $cache = $internal (); $flag = true
				Expr.If(
					Expr.Not(Expr.GetMember(EntityNames.MainTypeName, flagName)),
					Expr.Block(
						Expr.SetMember(
							EntityNames.MainTypeName,
							fieldName,
							Expr.Invoke(EntityNames.MainTypeName, originalName)
						),
						Expr.SetMember(EntityNames.MainTypeName, flagName, Expr.True())
					)
				),

				// $cache
				Expr.GetMember(EntityNames.MainTypeName, fieldName)
			);
		}

		private void createPureWrapper1(MethodEntity wrapper, string originalName)
		{
			var args = wrapper.GetArgumentTypes(Context);
			var argName = wrapper.Arguments[0].Name;

			var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
			var fieldType = typeof(Dictionary<,>).MakeGenericType(args[0], wrapper.ReturnType);

			CreateField(fieldName, fieldType, true);

			wrapper.Body = Expr.Block(

				// if ($dict == null) $dict = new Dictionary<$argType, $valueType> ()
				Expr.If(
					Expr.Equal(
						Expr.GetMember(EntityNames.MainTypeName, fieldName),
						Expr.Null()
					),
					Expr.Block(
						Expr.SetMember(
							EntityNames.MainTypeName, fieldName,
							Expr.New(fieldType)
						)
					)
				),

				// if(not $dict.ContainsKey key) $dict.Add ($internal arg)
				Expr.If(
					Expr.Not(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"ContainsKey",
							Expr.Get(argName)
						)
					),
					Expr.Block(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"Add",
							Expr.Get(argName),
							Expr.Invoke(EntityNames.MainTypeName, originalName, Expr.Get(argName))
						)
					)
				),

				// $dict[arg]
				Expr.GetIdx(
					Expr.GetMember(EntityNames.MainTypeName, fieldName),
					Expr.Get(argName)
				)
			);
		}

		private void createPureWrapperMany(MethodEntity wrapper, string originalName)
		{
			var args = wrapper.GetArgumentTypes(Context);

			var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
			var tupleType = FunctionalHelper.CreateTupleType(args);
			var fieldType = typeof(Dictionary<,>).MakeGenericType(tupleType, wrapper.ReturnType);

			CreateField(fieldName, fieldType, true);

			var argGetters = wrapper.Arguments.Select(a => (NodeBase)Expr.Get(a)).ToArray();
			var tupleName = "<args>";

			wrapper.Body = Expr.Block(

				// $tmp = new Tuple<...> $arg1 $arg2 ...
				Expr.Let(tupleName, Expr.New(tupleType, argGetters)),

				// if ($dict == null) $dict = new Dictionary<$tupleType, $valueType> ()
				Expr.If(
					Expr.Equal(
						Expr.GetMember(EntityNames.MainTypeName, fieldName),
						Expr.Null()
					),
					Expr.Block(
						Expr.SetMember(
							EntityNames.MainTypeName, fieldName,
							Expr.New(fieldType)
						)
					)
				),

				// if(not $dict.ContainsKey key) $dict.Add ($internal arg)
				Expr.If(
					Expr.Not(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"ContainsKey",
							Expr.Get(tupleName)
						)
					),
					Expr.Block(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"Add",
							Expr.Get(tupleName),
							Expr.Invoke(EntityNames.MainTypeName, originalName, argGetters)
						)
					)
				),

				// $dict[arg]
				Expr.GetIdx(
					Expr.GetMember(EntityNames.MainTypeName, fieldName),
					Expr.Get(tupleName)
				)

			);
		}

		private void createIterator(MethodEntityBase method)
		{
			var returnType = method.YieldStatements.Select(s => s.GetIteratorType(Context)).GetMostCommonType();

			var typeName = string.Format(EntityNames.IteratorTypeName, Context.GetClosureId());
			var type = Context.CreateType(typeName, null as string, true, false, true);
			type.Kind = TypeEntityKind.Iterator;
			type.Interfaces = new[]
			{
				typeof(IEnumerable<>).MakeGenericType(returnType),
				typeof(IEnumerator<>).MakeGenericType(returnType)
			};

			// main method
			var moveNext = type.CreateMethod("MoveNext", typeof(bool));
			moveNext.Body = Expr.Block(
				method.Body,
				Expr.False()
			);

			// todo: Current property

			// dispose
			var dispose = type.CreateMethod("Dispose", typeof(void));
			dispose.Body = Expr.Block(
				Expr.Dynamic(ct => ct.CurrentILGenerator.EmitNop())
			);

			// connect base method to iterator
			method.Body = Expr.Block(
				Expr.New(typeName)
			);
		}

		#endregion
	}
}
