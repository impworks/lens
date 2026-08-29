using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Resolver;
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
        /// Whether this scope has to own a closure: something declared at or below it is captured,
        /// or a lambda declared in it needs a class to live in.
        ///
        /// This is the analysis answer, decided without an ILGenerator or a TypeEntity in sight.
        /// The closure class that implements it is created later, by EmitSelf.
        /// </summary>
        public bool NeedsClosure { get; private set; }

        /// <summary>
        /// The type entity that represents current closure.
        /// </summary>
        public TypeEntity ClosureType { get; private set; }

        /// <summary>
        /// The closure type as it is referred to from the method that owns the closure.
        /// A closure declared inside a generic function is generic in that function's parameters,
        /// so it must be instantiated over them before it can be mentioned in a signature.
        ///
        /// A state machine refers to itself instead: MoveNext is a member of the machine class, so
        /// what it holds is the class applied to its own parameters, which only exists once the
        /// declaration has been emitted.
        /// </summary>
        public TypeEntry ClosureInstanceType => ClosureIsThis ? ClosureType.SelfType : _closureInstanceType;

        private TypeEntry _closureInstanceType;

        /// <summary>
        /// The generic parameters that the closure type forwards, in the terms of the method
        /// that owns the closure.
        /// </summary>
        private Type[] _closureSourceParameters;

        /// <summary>
        /// The generic parameters of the closure type itself.
        /// </summary>
        private Type[] _closureOwnParameters;

        /// <summary>
        /// Whether the closure instance is the method's own 'this' rather than a local variable.
        ///
        /// This is what a state machine is: MoveNext is a method on the class that holds the
        /// names, so the class does not have to be created and stored anywhere - it is already
        /// there, as the receiver.
        /// </summary>
        public bool ClosureIsThis { get; private set; }

        /// <summary>
        /// Whether every name at or below this scope has to live in the closure type rather than
        /// on the stack frame, because the frame does not survive between two statements.
        /// </summary>
        public bool IsMachineRoot { get; private set; }

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
                    argType = argType.ElementType;

                var local = new Local(arg.Name, argType, false, arg.IsRefArgument)
                {
                    ArgumentId = idx,
                    Declaration = arg
                };

                DeclareLocal(local);

                idx++;
            }
        }

        /// <summary>
        /// Adds a new local name to current scope.
        /// </summary>
        public Local DeclareLocal(string name, TypeEntry type, bool isConst, bool isRefArg = false)
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
        public Local DeclareImplicit(Context ctx, TypeEntry type, bool isConst)
        {
            var local = DeclareLocal(ctx.Unique.TempVariableName(), type, isConst);

            // a name the compiler invents while emitting arrives after this scope has already
            // declared its locals, so it has to claim a slot straight away. One invented while
            // binding is declared by EmitSelf along with every other local - and must not be
            // declared here, or binding would need an ILGenerator it should know nothing about.
            if (ctx.CurrentMethod?.Generator != null)
                local.LocalBuilder = ctx.CurrentMethod.Generator.DeclareLocal(type.Materialize());

            return local;
        }

        /// <summary>
        /// Finds a local name in current or any parent scopes.
        ///
        /// Returns the declaration itself, not a copy of it: "the variable x declared on line 12"
        /// has to be one object with one identity, or nothing can point at it.
        /// </summary>
        public Local FindLocal(string name)
        {
            var scope = this;
            while (scope != null)
            {
                if (scope.Locals.TryGetValue(name, out Local local))
                    return local;

                scope = scope.OuterScope;
            }

            return null;
        }

        /// <summary>
        /// Registers a name being referenced during closure detection.
        ///
        /// Pure analysis: it decides that a variable is captured and which scope will have to hold
        /// it, and creates nothing. There is deliberately no Context parameter - if this method
        /// could reach the context it could reach the assembly being built.
        /// </summary>
        /// <returns>True if the local name has been found. Otherwise false.</returns>
        public bool ReferenceLocal(string name)
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

                        local.IsClosured = true;
                        local.ClosureScope = scope.ClosureOwner();
                        local.ClosureScope.NeedsClosure = true;
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
        /// Declares that this scope's names belong to a state machine class, of which the method
        /// being compiled is a member.
        /// </summary>
        public void MakeMachineRoot(TypeEntity machineType)
        {
            IsMachineRoot = true;
            ClosureIsThis = true;
            NeedsClosure = true;
            ClosureType = machineType;
        }

        /// <summary>
        /// Records what the analysis of this scope concluded, once the whole scope has been walked.
        /// Names the closure fields but does not create them.
        /// </summary>
        public void AnalyzeSelf(Context ctx)
        {
            var machine = MachineOwner();

            foreach (var curr in Locals.Values)
            {
                if (IsUnrolledConstant(ctx, curr))
                    continue;

                if (machine != null)
                {
                    // a state machine's frame does not survive between two statements, so every
                    // name it holds has to be a field. This is the same hoisting a closure does,
                    // and it is deliberately the same mechanism: a name that is both captured and
                    // live across a resume point must end up in exactly one place, or a mutation
                    // through one view would be invisible through the other.
                    if (!curr.IsClosured)
                    {
                        curr.IsClosured = true;
                        curr.ClosureScope = machine;
                    }
                    else if (curr.ClosureScope != machine)
                    {
                        // the name lives in a closure of its own - a loop makes one per iteration -
                        // and that closure is held in a local the machine cannot carry across a
                        // resume point
                        Context.Error(CompilerMessages.YieldLoopClosure, curr.Name);
                    }
                }

                if (curr.IsClosured && curr.ClosureFieldName == null)
                    curr.ClosureFieldName = ctx.Unique.ClosureFieldName(curr.Name);
            }
        }

        /// <summary>
        /// Creates the entities that the analysis of this scope called for: the closure class and
        /// its fields, and an IL local for everything that stayed on the stack frame.
        /// </summary>
        public void EmitSelf(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;

            foreach (var curr in Locals.Values)
            {
                if (IsUnrolledConstant(ctx, curr))
                    continue;

                // a name whose type was still being worked out is settled by whatever reads it. One
                // that reaches here unsettled was never read, and needs a slot all the same
                curr.SealType();

                if (curr.IsClosured)
                {
                    // the owner may sit further out than this scope, and its own EmitSelf runs
                    // later, so its class has to be brought into being here
                    var closure = curr.ClosureScope;
                    closure.EnsureClosureType(ctx);

                    // a state machine's arguments already have their fields: the function that
                    // creates the machine has to fill them in, and it is compiled before anything
                    // here has run
                    if (closure.ClosureType.HasField(curr.ClosureFieldName))
                        continue;

                    var field = closure.ClosureType.CreateField(curr.ClosureFieldName, closure.SubstituteIntoClosure(curr.Type));
                    field.Kind = TypeContentsKind.Closure;
                }
                else if (curr.LocalBuilder == null)
                {
                    // an implicit local invented while emitting already has its slot; declaring a
                    // second one for it wasted a slot per temporary
                    curr.LocalBuilder = gen.DeclareLocal(curr.Type.Materialize());
                    ctx.DebugInfo?.NameLocal(curr.LocalBuilder, curr.Name);
                }
            }

            if (NeedsClosure)
            {
                EnsureClosureType(ctx);

                // the machine class is the outermost frame of its own MoveNext and is reached
                // through 'this', so it has neither a parent to affix to nor a local to live in
                if (ClosureIsThis)
                    return;

                DetectClosureParent();

                if (ClosureParent != null)
                {
                    // create "Parent" field in the closure type.
                    // both closure types forward the same set of enclosing parameters, so the
                    // parent is referred to through this closure's own ones
                    ClosureParent.EnsureClosureType(ctx);

                    var parentType = _closureOwnParameters == null
                        ? ClosureParent.ClosureType.TypeInfo
                        : TypeEntryCache.Of(ctx.Resolver.MakeGenericType(ClosureParent.ClosureType.TypeBuilder, _closureOwnParameters));

                    ClosureType.CreateField(EntityNames.ParentScopeFieldName, parentType);
                }

                ClosureVariable = gen.DeclareLocal(ClosureInstanceType.Materialize());
            }
        }

        /// <summary>
        /// Checks whether a name is a constant that gets substituted at its use sites, and so needs
        /// no storage at all.
        /// </summary>
        private static bool IsUnrolledConstant(Context ctx, Local local)
        {
            return local.IsConstant && local.IsImmutable && ctx.UnrollConstants;
        }

        /// <summary>
        /// Emits the code that loads the closure instance which contains the given local variable.
        /// </summary>
        /// <returns>The type of the closure instance that has been pushed onto the stack.</returns>
        public TypeEntry EmitClosureInstance(Context ctx, Local local)
        {
            var gen = ctx.CurrentMethod.Generator;
            var closure = local.ClosureScope;

            // find the closure within current method: it is stored in a local variable
            var scope = this;
            while (scope != null && scope != closure && scope.Kind != ScopeKind.LambdaRoot)
                scope = scope.OuterScope;

            if (scope == closure)
            {
                if (closure.ClosureIsThis)
                    gen.EmitLoadArgument(0);
                else
                    gen.EmitLoadLocal(closure.ClosureVariable);

                return closure.ClosureInstanceType;
            }

            if (scope == null)
                throw new InvalidOperationException($"Closure for variable '{local.Name}' is not found!");

            // the variable belongs to an outer method:
            // start from the closure passed as 'this' pointer and follow the parent references
            gen.EmitLoadArgument(0);

            var currentType = ctx.CurrentType.SelfType;

            var curr = FindScope(s => s.ClosureType != null, scope.OuterScope);
            while (curr != null && curr != closure)
            {
                // the parent's type is read off the field, so that each step of the chain stays
                // instantiated over the right generic arguments
                var parentField = ctx.ResolveField(currentType, EntityNames.ParentScopeFieldName);
                gen.EmitLoadField(parentField.FieldInfo);
                currentType = parentField.FieldType;

                curr = curr.ClosureParent;
            }

            if (curr == null)
                throw new InvalidOperationException($"Closure for variable '{local.Name}' is not found!");

            return currentType;
        }

        /// <summary>
        /// Declares a new anonymous method in the current closure class.
        /// </summary>
        public MethodEntity CreateClosureMethod(Context ctx, IEnumerable<FunctionArgument> args, TypeEntry returnType)
        {
            var scope = ClosureOwner();
            scope.NeedsClosure = true;
            var closure = scope.EnsureClosureType(ctx);

            // the signature belongs to the closure class, so it must be spelled in terms of that
            // class's own generic parameters rather than the enclosing method's
            var closureArgs = args?.Select(
                x => new FunctionArgument(x.Name, scope.SubstituteIntoClosure(x.GetArgumentType(ctx)), x.IsRefArgument)
            );

            var method = closure.CreateMethod(
                ctx.Unique.ClosureMethodName(ctx.CurrentMethod.Name),
                scope.SubstituteIntoClosure(returnType),
                closureArgs
            );

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

                if (scope.NeedsClosure)
                {
                    ClosureParent = scope;
                    ClosureParentIsRemote = isRemote;
                    return;
                }
            }
        }

        /// <summary>
        /// The scope that would own a closure holding the names of this one: an Unclosured scope
        /// shares its enclosing frame's closure rather than getting one of its own.
        ///
        /// A LambdaRoot and a FunctionRoot are both closured kinds, so the search never leaves the
        /// method it started in.
        /// </summary>
        private Scope ClosureOwner()
        {
            return FindScope(s => s.Kind != ScopeKind.Unclosured);
        }

        /// <summary>
        /// The state machine frame this scope belongs to, if any.
        ///
        /// The search stops at a lambda: a lambda declared inside an iterator is an ordinary method
        /// with an ordinary frame, and its own names have no reason to be hoisted.
        /// </summary>
        private Scope MachineOwner()
        {
            var curr = this;
            while (curr != null)
            {
                if (curr.IsMachineRoot)
                    return curr;

                if (curr.Kind == ScopeKind.LambdaRoot)
                    return null;

                curr = curr.OuterScope;
            }

            return null;
        }

        /// <summary>
        /// Creates the closure class of this scope, unless it already has one.
        /// This is the emission half: everything it touches is an assembly artefact.
        /// </summary>
        private TypeEntity EnsureClosureType(Context ctx)
        {
            if (ClosureType != null)
                return ClosureType;

            var name = ctx.Unique.ClosureName();

            // a closure inside a generic declaration holds values whose types mention its
            // parameters, so the closure class must be generic in them as well
            var enclosing = ctx.EnclosingGenericParameters();
            var forwarded = ctx.CloneGenericParameters(enclosing, name);

            ClosureType = ctx.CreateType(name, genericParameters: forwarded);
            ClosureType.Kind = TypeEntityKind.Closure;

            if (forwarded != null)
            {
                _closureSourceParameters = enclosing.Select(x => (Type) x.Builder).ToArray();
                _closureOwnParameters = forwarded.Select(x => (Type) x.Builder).ToArray();
                _closureInstanceType = TypeEntryCache.Of(ctx.Resolver.MakeGenericType(ClosureType.TypeBuilder, _closureSourceParameters));
            }
            else
            {
                _closureInstanceType = ClosureType.TypeInfo;
            }

            return ClosureType;
        }

        /// <summary>
        /// Rewrites a type expressed in the terms of the enclosing method's generic parameters
        /// into the terms of the closure type's own parameters, which is how the members of the
        /// closure class must be declared.
        /// </summary>
        private TypeEntry SubstituteIntoClosure(TypeEntry type)
        {
            if (_closureSourceParameters == null)
                return type;

            return TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(type.Materialize(), _closureSourceParameters, _closureOwnParameters, false));
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