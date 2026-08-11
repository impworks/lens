using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.Translations;

namespace Lens.Compiler.Entities
{
    internal partial class TypeEntity
    {
        #region Auto-generated entities

        /// <summary>
        /// Creates the body of Equals(T).
        /// </summary>
        private void CreateSpecificEquals()
        {
            var eq = CreateMethod("Equals", "bool", new[] {Expr.Arg("other", SelfType)});

            // var result = true
            eq.Body.Add(Expr.Var("result", Expr.True()));

            foreach (var f in _fields.Values)
            {
                var left = Expr.GetMember(Expr.This(), f.Name);
                var right = Expr.GetMember(Expr.Get("other"), f.Name);

                var fieldType = FieldTypeOf(f);
                var isSeq = fieldType.IsGenericType && TypeEntryCache.Of(fieldType).Implements(Context.Resolver, TypeEntryCache.Of(typeof(IEnumerable<>)), true);
                var expr = isSeq
                    ? Expr.Invoke("Enumerable", "SequenceEqual", left, right)
                    : Expr.Invoke(DefaultComparerOf(fieldType), "Equals", left, right);

                eq.Body.Add(
                    Expr.Set(
                        "result",
                        Expr.And(Expr.Get("result"), expr)
                    )
                );
            }

            eq.Body.Add(Expr.Get("result"));
        }

        /// <summary>
        /// Returns the type of a field, resolving its signature if that has not happened yet.
        /// </summary>
        private Type FieldTypeOf(FieldEntity field)
        {
            return field.Type ?? (field.Type = Context.ResolveType(field.TypeSignature));
        }

        /// <summary>
        /// Builds an expression for EqualityComparer&lt;T&gt;.Default.
        ///
        /// A field whose type is a type parameter may end up being a value type, a reference type
        /// or a Nullable&lt;&gt;, and the correct comparison differs in each case. This is the same
        /// reason C# routes generated equality through the default comparer.
        /// </summary>
        private NodeBase DefaultComparerOf(Type fieldType)
        {
            var comparerType = Context.Resolver.MakeGenericType(typeof(EqualityComparer<>), new[] {fieldType});
            return Expr.GetMember(comparerType, "Default");
        }

        /// <summary>
        /// Creates the body of Equals(object).
        /// </summary>
        private void CreateGenericEquals()
        {
            var eq = CreateMethod(
                "Equals",
                "bool",
                new[] {Expr.Arg<object>("obj")},
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
                                    Expr.Cast(Expr.Get("obj"), SelfType)
                                )
                            )
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Creates the body of GetHashCode().
        /// </summary>
        private void CreateGetHashCode()
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

            // result ^= comparer.GetHashCode(<field>) * 397
            var id = 0;
            foreach (var f in _fields.Values)
            {
                var fieldType = FieldTypeOf(f);
                NodeBase expr;
                if (TypeEntryCache.Of(fieldType).IsIntegerType())
                {
                    expr = Expr.GetMember(Expr.This(), f.Name);
                }
                else
                {
                    // the default comparer knows how to hash a null, a boxed value type
                    // and a Nullable<> alike, which a bare GetHashCode call does not
                    expr = Expr.Invoke(
                        DefaultComparerOf(fieldType),
                        "GetHashCode",
                        Expr.GetMember(Expr.This(), f.Name)
                    );
                }

                if (id < _fields.Count - 1)
                    expr = Expr.Mult(expr, Expr.Int(397));

                ghc.Body.Add(
                    Expr.Set("result", Expr.Xor(Expr.Get("result"), expr))
                );

                id++;
            }

            ghc.Body.Add(Expr.Get("result"));
        }

        /// <summary>
        /// Creates a wrapper for the pure method that contains the value cache.
        /// </summary>
        private void CreatePureWrapper(MethodEntity method)
        {
            if (TypeEntryCache.Of(method.ReturnType).IsVoid())
                Context.Error(CompilerMessages.PureFunctionReturnUnit, method.Name);

            var pureName = string.Format(EntityNames.PureMethodNameTemplate, method.Name);

            // the internal method repeats the signature of the wrapper, so a generic one needs
            // generic parameters of its own. Its signature is therefore rebuilt from the source
            // signatures, which resolve against whichever parameters are in scope at the time.
            var pureArgs = method.Arguments?.Values
                                 .Select(x => new FunctionArgument(x.Name, x.TypeSignature, x.IsRefArgument))
                                 .ToArray();

            var pure = CreateMethod(pureName, method.ReturnTypeSignature, pureArgs, true, prepare: !method.IsGeneric);
            pure.GenericParameters = Context.CloneGenericParameters(method.GenericParameters, pureName);
            pure.Body = method.Body;

            var argCount = method.Arguments != null
                ? method.Arguments.Count
                : method.ArgumentTypes.Length;

            if (argCount >= 8)
                Context.Error(CompilerMessages.PureFunctionTooManyArgs, method.Name);

            var cache = new PureCache(this, method);

            if (argCount == 0)
                CreatePureWrapper0(cache, method, pureName);
            else if (argCount == 1)
                CreatePureWrapper1(cache, method, pureName);
            else
                CreatePureWrapperMany(cache, method, pureName);
        }

        /// <summary>
        /// The place where a pure function's memoized values live.
        ///
        /// For an ordinary function that is a set of static fields on the main type. A generic
        /// function cannot use those: a method's type parameters may not appear in the type of a
        /// field of an unrelated class, and the cache has to be per-instantiation anyway. Such a
        /// function gets a holder class generic in its own parameters instead, which is what C#
        /// does for exactly the same problem.
        /// </summary>
        private class PureCache
        {
            #region Constructor

            public PureCache(TypeEntity mainType, MethodEntity method)
            {
                _owner = mainType;

                if (!method.IsGeneric)
                    return;

                // the internal method has generic parameters of its own that appear nowhere but in
                // its signature, so an unreferenced one could never be inferred: the wrapper always
                // passes its own parameters through explicitly
                _typeHints = method.GenericParameters.Select(x => (TypeSignature) x.Name).ToArray();

                var name = string.Format(EntityNames.PureMethodCacheTypeNameTemplate, method.Name);
                var ctx = mainType.Context;

                var parameters = ctx.CloneGenericParameters(method.GenericParameters, name);
                _owner = ctx.CreateType(name, genericParameters: parameters);
                _owner.Kind = TypeEntityKind.Closure;

                _sourceParameters = method.GenericParameters.Select(x => (Type) x.Builder).ToArray();
                _ownParameters = parameters.Select(x => (Type) x.Builder).ToArray();

                _ownerType = ctx.Resolver.MakeGenericType(_owner.TypeBuilder, _sourceParameters);
            }

            #endregion

            #region Fields

            private readonly TypeEntity _owner;
            private readonly Type _ownerType;
            private readonly Type[] _sourceParameters;
            private readonly Type[] _ownParameters;
            private readonly TypeSignature[] _typeHints;

            #endregion

            #region Methods

            /// <summary>
            /// Declares a cache field, rewriting its type into the terms of the holder class.
            /// </summary>
            public void CreateField(string name, Type type)
            {
                _owner.CreateField(name, Substitute(type), true);
            }

            /// <summary>
            /// Builds an invocation of the internal method that actually computes the value.
            /// </summary>
            public NodeBase Invoke(string pureName, params NodeBase[] args)
            {
                return _typeHints == null
                    ? Expr.Invoke(EntityNames.MainTypeName, pureName, args)
                    : Expr.Invoke(Expr.GetMember(EntityNames.MainTypeName, pureName, _typeHints), args);
            }

            /// <summary>
            /// Builds an expression that reads a cache field.
            /// </summary>
            public NodeBase Get(string name)
            {
                return _ownerType == null
                    ? Expr.GetMember(EntityNames.MainTypeName, name)
                    : Expr.GetMember(_ownerType, name);
            }

            /// <summary>
            /// Builds an expression that writes a cache field.
            /// </summary>
            public NodeBase Set(string name, NodeBase value)
            {
                return _ownerType == null
                    ? Expr.SetMember(EntityNames.MainTypeName, name, value)
                    : Expr.SetMember(_ownerType, name, value);
            }

            #endregion

            #region Helpers

            /// <summary>
            /// Rewrites a type expressed in the function's parameters into the holder's own ones.
            /// </summary>
            private Type Substitute(Type type)
            {
                return _sourceParameters == null
                    ? type
                    : GenericHelper.ApplyGenericArguments(type, _sourceParameters, _ownParameters, false);
            }

            #endregion
        }

        /// <summary>
        /// Creates a pure wrapper for parameterless function.
        /// </summary>
        private void CreatePureWrapper0(PureCache cache, MethodEntity wrapper, string pureName)
        {
            var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
            var flagName = string.Format(EntityNames.PureMethodCacheFlagNameTemplate, wrapper.Name);

            cache.CreateField(fieldName, wrapper.ReturnType);
            cache.CreateField(flagName, typeof(bool));

            wrapper.Body = Expr.Block(
                ScopeKind.FunctionRoot,

                // if (not $flag) $cache = $internal (); $flag = true
                Expr.If(
                    Expr.Not(cache.Get(flagName)),
                    Expr.Block(
                        cache.Set(
                            fieldName,
                            cache.Invoke(pureName)
                        ),
                        cache.Set(flagName, Expr.True())
                    )
                ),

                // $cache
                cache.Get(fieldName)
            );
        }

        /// <summary>
        /// Creates a pure wrapper for function with 1 argument.
        /// </summary>
        private void CreatePureWrapper1(PureCache cache, MethodEntity wrapper, string pureName)
        {
            var args = wrapper.GetArgumentTypes(Context);
            var argName = wrapper.Arguments[0].Name;

            var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
            var fieldType = Context.Resolver.MakeGenericType(typeof(Dictionary<,>), new[] {args[0], wrapper.ReturnType});

            cache.CreateField(fieldName, fieldType);

            wrapper.Body = Expr.Block(
                ScopeKind.FunctionRoot,

                // if ($dict == null) $dict = new Dictionary<$argType, $valueType> ()
                Expr.If(
                    Expr.Equal(
                        cache.Get(fieldName),
                        Expr.Null()
                    ),
                    Expr.Block(
                        cache.Set(
                            fieldName,
                            Expr.New(fieldType)
                        )
                    )
                ),

                // if(not $dict.ContainsKey key) $dict.Add ($internal arg)
                Expr.If(
                    Expr.Not(
                        Expr.Invoke(
                            cache.Get(fieldName),
                            "ContainsKey",
                            Expr.Get(argName)
                        )
                    ),
                    Expr.Block(
                        Expr.Invoke(
                            cache.Get(fieldName),
                            "Add",
                            Expr.Get(argName),
                            cache.Invoke(pureName, Expr.Get(argName))
                        )
                    )
                ),

                // $dict[arg]
                Expr.GetIdx(
                    cache.Get(fieldName),
                    Expr.Get(argName)
                )
            );
        }

        /// <summary>
        /// Creates a pure wrapper for function with 2 and more arguments.
        /// </summary>
        private void CreatePureWrapperMany(PureCache cache, MethodEntity wrapper, string pureName)
        {
            var args = wrapper.GetArgumentTypes(Context);

            var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
            var tupleType = FunctionalHelper.CreateTupleType(args);
            var fieldType = Context.Resolver.MakeGenericType(typeof(Dictionary<,>), new[] {tupleType, wrapper.ReturnType});

            cache.CreateField(fieldName, fieldType);

            var argGetters = wrapper.Arguments.Select(a => (NodeBase) Expr.Get(a)).ToArray();
            var tupleName = "<args>";

            wrapper.Body = Expr.Block(
                ScopeKind.FunctionRoot,

                // $tmp = new Tuple<...> $arg1 $arg2 ...
                Expr.Let(tupleName, Expr.New(tupleType, argGetters)),

                // if ($dict == null) $dict = new Dictionary<$tupleType, $valueType> ()
                Expr.If(
                    Expr.Equal(
                        cache.Get(fieldName),
                        Expr.Null()
                    ),
                    Expr.Block(
                        cache.Set(
                            fieldName,
                            Expr.New(fieldType)
                        )
                    )
                ),

                // if(not $dict.ContainsKey key) $dict.Add ($internal arg)
                Expr.If(
                    Expr.Not(
                        Expr.Invoke(
                            cache.Get(fieldName),
                            "ContainsKey",
                            Expr.Get(tupleName)
                        )
                    ),
                    Expr.Block(
                        Expr.Invoke(
                            cache.Get(fieldName),
                            "Add",
                            Expr.Get(tupleName),
                            cache.Invoke(pureName, argGetters)
                        )
                    )
                ),

                // $dict[arg]
                Expr.GetIdx(
                    cache.Get(fieldName),
                    Expr.Get(tupleName)
                )
            );
        }

        #endregion
    }
}