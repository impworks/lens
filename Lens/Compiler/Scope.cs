using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// A scope slice that contains a list of local variables.
    /// </summary>
    internal class Scope
    {
        #region Constructor

        public Scope(ScopeKind kind)
        {
            Locals = new Dictionary<string, Local>();
            Kind = kind;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The list of names in current scope.
        /// </summary>
        public readonly Dictionary<string, Local> Locals;

        /// <summary>
        /// The scope that contains the current scope.
        /// </summary>
        public Scope OuterScope;

        /// <summary>
        /// Checks if the the scope is root for a particular method.
        /// </summary>
        public readonly ScopeKind Kind;

        /// <summary>
        /// The type entity that represents current closure.
        /// </summary>
        public TypeEntity ClosureType { get; private set; }

        /// <summary>
        /// The local variable in which the closure is saved.
        /// </summary>
        public LocalBuilder ClosureVariable { get; private set; }

        /// <summary>
        /// The closest scope that owns the closure which contains current closure.
        /// Is null when the current closure is the outermost one in its method.
        /// </summary>
        public Scope ClosureParent { get; private set; }

        /// <summary>
        /// Checks if the parent closure belongs to an outer method and therefore
        /// must be loaded from the 'this' pointer rather than from a local variable.
        /// </summary>
        public bool ClosureParentIsRemote { get; private set; }

        /// <summary>
        /// The nearest scope which contains a closure.
        /// </summary>
        public Scope ActiveClosure => FindScope(x => x.ClosureType != null);

        #endregion

        #region Methods

        /// <summary>
        /// Imports all arguments into the scope.
        /// </summary>
        public void RegisterArguments(Context ctx, bool isStatic, IEnumerable<FunctionArgument> args)
        {
            if (args == null)
                return;

            var idx = isStatic ? 0 : 1;
            foreach (var arg in args)
            {
                if (arg.Name == "_")
                    arg.Name = ctx.Unique.AnonymousArgName();

                var argType = arg.GetArgumentType(ctx);
                if (argType.IsByRef)
                    argType = argType.GetElementType();

                var local = new Local(arg.Name, argType, false, arg.IsRefArgument) {ArgumentId = idx};
                DeclareLocal(local);

                idx++;
            }
        }

        /// <summary>
        /// Adds a new local name to current scope.
        /// </summary>
        public Local DeclareLocal(string name, Type type, bool isConst, bool isRefArg = false)
        {
            var local = new Local(name, type, isConst, isRefArg);
            DeclareLocal(local);
            return local;
        }

        /// <summary>
        /// Adds a new local name to current scope.
        /// </summary>
        public void DeclareLocal(Local local)
        {
            if (Locals.ContainsKey(local.Name))
                throw new LensCompilerException(string.Format(CompilerMessages.VariableDefined, local.Name));

            Locals[local.Name] = local;
        }

        /// <summary>
        /// Creates a new implicit local variable or constant.
        /// </summary>
        public Local DeclareImplicit(Context ctx, Type type, bool isConst)
        {
            var local = DeclareLocal(ctx.Unique.TempVariableName(), type, isConst);
            local.LocalBuilder = ctx.CurrentMethod.Generator.DeclareLocal(type);
            return local;
        }

        /// <summary>
        /// Finds a local name in current or any parent scopes.
        /// </summary>
        public Local FindLocal(string name)
        {
            var scope = this;
            while (scope != null)
            {
                // try get the variable itself
                if (scope.Locals.TryGetValue(name, out Local local))
                    return local.GetCopy();

                scope = scope.OuterScope;
            }

            return null;
        }

        /// <summary>
        /// Registers a name being referenced during closure detection.
        /// </summary>
        /// <returns>True if the local name has been found. Otherwise false.</returns>
        public bool ReferenceLocal(Context ctx, string name)
        {
            var scope = this;
            var isClosured = false;
            while (scope != null)
            {
                if (scope.Locals.TryGetValue(name, out var local))
                {
                    if (isClosured)
                    {
                        if (local.IsRefArgument)
                            Context.Error(CompilerMessages.ClosureRef, local.Name);

                        CreateClosureType(ctx, scope);
                        local.IsClosured = true;
                    }

                    return true;
                }

                if (scope.Kind == ScopeKind.LambdaRoot)
                    isClosured = true;

                scope = scope.OuterScope;
            }

            return false;
        }

        /// <summary>
        /// Creates entities for current scope when it has been left.
        /// </summary>
        public void FinalizeSelf(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;
            var closure = FindScope(s => s.ClosureType != null);

            // create entities for variables to be excluded
            foreach (var curr in Locals.Values)
            {
                if (curr.IsConstant && curr.IsImmutable && ctx.Options.UnrollConstants)
                    continue;

                if (curr.IsClosured)
                {
                    curr.ClosureFieldName = ctx.Unique.ClosureFieldName(curr.Name);
                    curr.ClosureScope = closure;
                    var field = closure.ClosureType.CreateField(curr.ClosureFieldName, curr.Type);
                    field.Kind = TypeContentsKind.Closure;
                }
                else
                {
                    curr.LocalBuilder = gen.DeclareLocal(curr.Type);
                }
            }

            if (ClosureType != null)
            {
                DetectClosureParent();

                if (ClosureParent != null)
                {
                    // create "Parent" field in the closure type
                    var parentType = ClosureParent.ClosureType;
                    ClosureType.CreateField(EntityNames.ParentScopeFieldName, parentType.TypeInfo);
                }

                ClosureVariable = ctx.CurrentMethod.Generator.DeclareLocal(ClosureType.TypeInfo);
            }
        }

        /// <summary>
        /// Emits the code that loads the closure instance which contains the given local variable.
        /// </summary>
        public void EmitClosureInstance(Context ctx, Local local)
        {
            var gen = ctx.CurrentMethod.Generator;
            var closure = local.ClosureScope;

            // find the closure within current method: it is stored in a local variable
            var scope = this;
            while (scope != null && scope != closure && scope.Kind != ScopeKind.LambdaRoot)
                scope = scope.OuterScope;

            if (scope == closure)
            {
                gen.EmitLoadLocal(closure.ClosureVariable);
                return;
            }

            if (scope == null)
                throw new InvalidOperationException($"Closure for variable '{local.Name}' is not found!");

            // the variable belongs to an outer method:
            // start from the closure passed as 'this' pointer and follow the parent references
            gen.EmitLoadArgument(0);

            var curr = FindScope(s => s.ClosureType != null, scope.OuterScope);
            while (curr != null && curr != closure)
            {
                var parentField = curr.ClosureType.ResolveField(EntityNames.ParentScopeFieldName);
                gen.EmitLoadField(parentField.FieldBuilder);

                curr = curr.ClosureParent;
            }

            if (curr == null)
                throw new InvalidOperationException($"Closure for variable '{local.Name}' is not found!");
        }

        /// <summary>
        /// Declares a new anonymous method in the current closure class.
        /// </summary>
        public MethodEntity CreateClosureMethod(Context ctx, IEnumerable<FunctionArgument> args, Type returnType)
        {
            var closure = CreateClosureType(ctx);
            var method = closure.CreateMethod(ctx.Unique.ClosureMethodName(ctx.CurrentMethod.Name), returnType.FullName, args);
            method.Kind = TypeContentsKind.Closure;
            return method;
        }

        /// <summary>
        /// Applies local names to a temporary scope. Is useful for expanding nodes that introduce a variable.
        /// </summary>
        public static T WithTempLocals<T>(Context ctx, Func<T> action, params Local[] vars)
        {
            var scope = new Scope(ScopeKind.Unclosured);
            foreach (var curr in vars)
                scope.DeclareLocal(curr);

            ctx.EnterScope(scope);
            var result = action();
            ctx.ExitScope();

            return result;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Finds closest scope by a condition.
        /// </summary>
        private Scope FindScope(Func<Scope, bool> condition, Scope start = null)
        {
            var curr = start ?? this;
            while (curr != null)
            {
                if (condition(curr))
                    return curr;

                curr = curr.OuterScope;
            }

            return null;
        }

        /// <summary>
        /// Finds the closure that contains the current one, if any.
        /// Note that closures do not map to methods one-to-one: a loop within a method
        /// creates a closure of its own, so that each iteration gets a fresh set of variables.
        /// </summary>
        private void DetectClosureParent()
        {
            var scope = this;
            var isRemote = false;

            while (true)
            {
                // the current method's boundary has been crossed:
                // the parent closure is only reachable via the 'this' pointer
                if (scope.Kind == ScopeKind.LambdaRoot)
                    isRemote = true;

                scope = scope.OuterScope;
                if (scope == null)
                    return;

                if (scope.ClosureType != null)
                {
                    ClosureParent = scope;
                    ClosureParentIsRemote = isRemote;
                    return;
                }
            }
        }

        /// <summary>
        /// Creates a closure type in the closest appropriate scope.
        /// </summary>
        private TypeEntity CreateClosureType(Context ctx, Scope scope = null)
        {
            var cscope = FindScope(s => s.Kind != ScopeKind.Unclosured, scope ?? this);
            if (cscope.ClosureType == null)
            {
                cscope.ClosureType = ctx.CreateType(ctx.Unique.ClosureName());
                cscope.ClosureType.Kind = TypeEntityKind.Closure;
            }
            return cscope.ClosureType;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return string.Format(
                "{0}({1})",
                Kind,
                Locals.Count > 0
                    ? string.Join(", ", Locals.Keys) 
                    : "none"
            );
        }

        #endregion
    }

    /// <summary>
    /// Declares the kind of scope, which affects the way its parenthood is instantiated.
    /// </summary>
    internal enum ScopeKind
    {
        /// <summary>
        /// Scope has no 
        /// </summary>
        Unclosured,

        /// <summary>
        /// Scope is the root of a static user-defined function (including Main).
        /// Closure parent is not used.
        /// </summary>
        FunctionRoot,

        /// <summary>
        /// Scope is within a loop.
        /// Closure parent is loaded from a local variable.
        /// </summary>
        Loop,

        /// <summary>
        /// Scope is the root of a lambda function.
        /// Closure parent is loaded from 'this' pointer.
        /// </summary>
        LambdaRoot,

        /// <summary>
        /// Special case for match node's root scope.
        /// Makes each of the nested expression blocks explicitly return the value.
        /// </summary>
        MatchRoot
    }
}