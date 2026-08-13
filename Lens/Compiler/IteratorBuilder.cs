using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler.Entities;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.Resolver;
using Lens.SyntaxTree.Internals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
    /// <summary>
    /// Rewrites a function that yields into a class that implements the iterator protocol.
    ///
    /// The function keeps its name and signature and becomes a factory: it creates the machine,
    /// hands it the arguments and returns it. Everything the function used to do moves into
    /// MoveNext, whose body is the lowered original with a dispatch switch in front of it.
    ///
    /// The class is a class rather than a struct on purpose. C# uses a struct in release builds to
    /// save the allocation on the synchronous path, and pays for it with the boxing dance around
    /// MoveNextRunner and a much worse debugging experience. LENS is glue code and is not on
    /// anyone's hot path.
    /// </summary>
    internal class IteratorBuilder
    {
        #region Constructor

        private IteratorBuilder(Context ctx, FunctionNode node, MethodEntity method)
        {
            _ctx = ctx;
            _node = node;
            _method = method;
        }

        #endregion

        #region Fields

        private readonly Context _ctx;
        private readonly FunctionNode _node;
        private readonly MethodEntity _method;

        private TypeEntity _machine;
        private TypeSignature _machineSignature;
        private TypeSignature _elementSignature;

        /// <summary>
        /// The label that resumes each suspension point, in the order the states are numbered.
        /// </summary>
        private readonly List<LabelRef> _resumePoints = new List<LabelRef>();

        /// <summary>
        /// The field each of the function's arguments is carried in.
        /// </summary>
        private readonly List<Tuple<FunctionArgument, string>> _argumentFields = new List<Tuple<FunctionArgument, string>>();

        #endregion

        #region Entry point

        /// <summary>
        /// Checks whether a function hands values to a consumer rather than returning one.
        /// </summary>
        public static bool IsIterator(FunctionNode node)
        {
            return Lowerer.ContainsYieldAnywhere(node.Body);
        }

        /// <summary>
        /// Turns the function into a factory and moves its body into a state machine.
        /// </summary>
        public static void Build(Context ctx, FunctionNode node, MethodEntity method)
        {
            new IteratorBuilder(ctx, node, method).BuildCore();
        }

        private void BuildCore()
        {
            Validate();

            _machine = CreateMachineType();

            var moveNextBody = BuildMoveNextBody();
            var scope = _ctx.ScopeOf(moveNextBody);
            scope.MakeMachineRoot(_machine);

            DeclareArgumentNames(scope);
            DeclareMachineMembers(moveNextBody);

            _method.Body = BuildFactoryBody();
        }

        #endregion

        #region Validation

        private void Validate()
        {
            // a yield inside a lambda would be the lambda's own, and a lambda has no way to declare
            // that it returns a sequence
            Lowerer.CheckNoNestedYields(_node.Body);

            if (_node.IsPure)
                Error(CompilerMessages.IteratorPure, _node.Name);

            if (_node.TypeParameters != null && _node.TypeParameters.Count > 0)
                Error(CompilerMessages.IteratorGeneric, _node.Name);

            foreach (var arg in _node.Arguments)
                if (arg.IsRefArgument)
                    Error(CompilerMessages.ClosureRef, arg.Name);

            var returnType = _node.ReturnTypeSignature;
            if (returnType == null || string.IsNullOrEmpty(returnType.FullSignature))
                Error(CompilerMessages.IteratorReturnTypeRequired, _node.Name);

            _elementSignature = GetElementSignature(returnType);
            if (_elementSignature == null)
                Error(CompilerMessages.IteratorReturnTypeMismatch, _node.Name, returnType.FullSignature);
        }

        /// <summary>
        /// Reads the item type out of a sequence signature, or returns null if the signature does
        /// not describe a sequence at all.
        ///
        /// Nothing is resolved here. The machine is built out of the parse tree, before any type
        /// has a meaning, so the item type travels as a signature and is resolved along with every
        /// other member of the generated class.
        /// </summary>
        private static TypeSignature GetElementSignature(TypeSignature signature)
        {
            // 'int~' is how LENS spells IEnumerable<int>
            if (signature.Postfix == "~")
                return signature.Arguments[0];

            if (signature.Arguments == null || signature.Arguments.Length != 1)
                return null;

            var name = signature.Name;
            return name == "IEnumerable" || name.EndsWith(".IEnumerable")
                ? signature.Arguments[0]
                : null;
        }

        #endregion

        #region The machine class

        private TypeEntity CreateMachineType()
        {
            var name = _ctx.Unique.StateMachineName();
            _machineSignature = new TypeSignature(name);

            var type = _ctx.CreateType(name, isSealed: true, prepare: false);
            _machine = type;

            type.Kind = TypeEntityKind.Closure;
            type.InterfaceSignatures = new[]
            {
                Sequence(_elementSignature),
                Enumerator(_elementSignature),
                new TypeSignature("System.Collections.IEnumerable"),
                new TypeSignature("System.Collections.IEnumerator"),
                new TypeSignature("System.IDisposable")
            };

            CreateField(EntityNames.StateFieldName, new TypeSignature("int"));
            CreateField(EntityNames.CurrentFieldName, _elementSignature);

            return type;
        }

        /// <summary>
        /// Gives each of the function's arguments a field, before anything is bound.
        ///
        /// The names cannot wait for the closure analysis to invent them: the factory is compiled
        /// in the same round as MoveNext and possibly before it, and it has to name the fields it
        /// fills in.
        /// </summary>
        private void DeclareArgumentNames(Scope scope)
        {
            foreach (var arg in _node.Arguments)
            {
                var fieldName = _ctx.Unique.ClosureFieldName(arg.Name);
                var type = arg.GetArgumentType(_ctx);

                CreateField(fieldName, type);
                _argumentFields.Add(Tuple.Create(arg, fieldName));

                // the argument is an ordinary name inside MoveNext, already hoisted: the body goes
                // on saying 'max' and the closure machinery turns that into a field access
                scope.DeclareLocal(
                    new Local(arg.Name, type)
                    {
                        IsClosured = true,
                        ClosureScope = scope,
                        ClosureFieldName = fieldName,
                        Declaration = arg
                    }
                );
            }
        }

        private void DeclareMachineMembers(CodeBlockNode moveNextBody)
        {
            CreateMethod("MoveNext", new TypeSignature("bool"), moveNextBody);

            // IEnumerator<T>.Current, implemented by matching the interface method's name and
            // signature - which is why the non-generic one below needs a name of its own
            CreateMethod(
                "get_Current",
                _elementSignature,
                Block(Expr.GetMember(Expr.This(), EntityNames.CurrentFieldName))
            );

            CreateMethod(
                EntityNames.NonGenericCurrentGetterName,
                new TypeSignature("object"),
                Block(Expr.GetMember(Expr.This(), EntityNames.CurrentFieldName)),
                typeof(IEnumerator).GetProperty("Current").GetGetMethod()
            );

            CreateMethod("GetEnumerator", Enumerator(_elementSignature), BuildFreshMachineBody(fromThis: true));

            CreateMethod(
                EntityNames.NonGenericGetEnumeratorName,
                new TypeSignature("System.Collections.IEnumerator"),
                Block(Expr.Invoke(Expr.This(), "GetEnumerator")),
                typeof(IEnumerable).GetMethod("GetEnumerator")
            );

            CreateMethod("Dispose", new TypeSignature("Void"), Block(SetState(Expr.Int(FinishedState))));
            CreateMethod("Reset", new TypeSignature("Void"), Block(Expr.Throw("System.NotSupportedException")));
        }

        #endregion

        #region Method bodies

        /// <summary>
        ///     goto resume_k if state == k         (for every suspension point)
        ///     goto done if state &lt;&gt; 0
        ///     state = -1
        ///     [the lowered body, whose yields jump back here]
        /// done:
        ///     state = -1
        ///     false
        /// </summary>
        private CodeBlockNode BuildMoveNextBody()
        {
            var lowerer = new Lowerer(_ctx, EmitYield);
            var body = lowerer.Lower(_node.Body, false);

            var doneLabel = new LabelRef("done");
            var dispatch = new List<NodeBase>();

            for (var idx = 0; idx < _resumePoints.Count; idx++)
                dispatch.Add(new GotoNode(_resumePoints[idx], Expr.Equal(GetState(), Expr.Int(idx + 1))));

            dispatch.Add(new GotoNode(doneLabel, Expr.NotEqual(GetState(), Expr.Int(InitialState))));

            // the machine is running from here on: should the body throw, a further MoveNext must
            // find a finished machine rather than resume into the statement that failed
            dispatch.Add(SetState(Expr.Int(FinishedState)));

            body.Statements.InsertRange(0, dispatch);
            body.Add(new LabelNode(doneLabel));
            body.Add(SetState(Expr.Int(FinishedState)));
            body.Add(Expr.False());

            return body;
        }

        /// <summary>
        ///     current = value
        ///     state = k
        ///     return true
        /// resume_k:
        ///     state = -1
        /// </summary>
        private void EmitYield(NodeBase value, List<NodeBase> output)
        {
            var state = _resumePoints.Count + 1;
            var label = new LabelRef("resume_" + state);
            _resumePoints.Add(label);

            output.Add(Expr.SetMember(Expr.This(), EntityNames.CurrentFieldName, value));
            output.Add(SetState(Expr.Int(state)));
            output.Add(new ReturnValueNode(Expr.True()));
            output.Add(new LabelNode(label));
            output.Add(SetState(Expr.Int(FinishedState)));
        }

        /// <summary>
        /// The body of the function itself, which now only builds a machine and hands it over.
        /// </summary>
        private CodeBlockNode BuildFactoryBody()
        {
            return BuildFreshMachineBody(fromThis: false);
        }

        /// <summary>
        ///     var m = new machine ()
        ///     m.arg = [the argument, or this.arg]
        ///     m
        ///
        /// GetEnumerator always hands out a fresh machine rather than returning itself on the first
        /// call. C# checks the thread id and reuses the instance to save an allocation; that saves
        /// one allocation per iteration of an already-allocating protocol and costs a subtle piece
        /// of state that has to be right.
        /// </summary>
        private CodeBlockNode BuildFreshMachineBody(bool fromThis)
        {
            var name = _ctx.Unique.TempVariableName();
            var block = new CodeBlockNode(ScopeKind.FunctionRoot);

            block.Add(Expr.Var(name, Expr.New(_machineSignature)));

            foreach (var curr in _argumentFields)
            {
                var value = fromThis
                    ? Expr.GetMember(Expr.This(), curr.Item2)
                    : Expr.Get(curr.Item1.Name) as NodeBase;

                block.Add(Expr.SetMember(Expr.Get(name), curr.Item2, value));
            }

            block.Add(Expr.Get(name));
            return block;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The state of a machine that has been created but not started.
        /// </summary>
        private const int InitialState = 0;

        /// <summary>
        /// The state of a machine that is either running or done, both of which mean the same
        /// thing to a MoveNext that arrives: there is nowhere to resume to.
        /// </summary>
        private const int FinishedState = -1;

        private static TypeSignature Sequence(TypeSignature element)
        {
            return new TypeSignature("System.Collections.Generic.IEnumerable", element);
        }

        private static TypeSignature Enumerator(TypeSignature element)
        {
            return new TypeSignature("System.Collections.Generic.IEnumerator", element);
        }

        private static NodeBase GetState()
        {
            return Expr.GetMember(Expr.This(), EntityNames.StateFieldName);
        }

        private static NodeBase SetState(NodeBase value)
        {
            return Expr.SetMember(Expr.This(), EntityNames.StateFieldName, value);
        }

        private static CodeBlockNode Block(params NodeBase[] statements)
        {
            var block = new CodeBlockNode(ScopeKind.FunctionRoot);
            block.AddRange(statements);
            return block;
        }

        private void CreateField(string name, TypeSignature type)
        {
            _machine.CreateField(name, type, prepare: false).Kind = TypeContentsKind.Closure;
        }

        private void CreateField(string name, TypeEntry type)
        {
            _machine.CreateField(name, type, prepare: false).Kind = TypeContentsKind.Closure;
        }

        private void CreateMethod(string name, TypeSignature returnType, CodeBlockNode body, System.Reflection.MethodInfo explicitOverride = null)
        {
            var method = _machine.CreateMethod(name, returnType, (IEnumerable<FunctionArgument>) null, isStatic: false, isVirtual: true, prepare: false);
            method.Kind = TypeContentsKind.AutoGenerated;
            method.Body = body;
            method.ExplicitOverride = explicitOverride;
        }

        [ContractAnnotation("=> halt")]
        private static void Error(string message, params object[] args)
        {
            throw new LensCompilerException(string.Format(message, args));
        }

        #endregion
    }
}
