using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Internals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
    /// <summary>
    /// The half of a state machine that an iterator and an async function have in common.
    ///
    /// Both need the same thing: a class whose fields hold what used to be the frame, a MoveNext
    /// that resumes at a numbered state, and a function that has been reduced to creating the
    /// machine and handing it over. Only the driving protocol differs - IEnumerator for one, a task
    /// and an awaiter for the other - which is why it is one feature with two front-ends rather
    /// than two features.
    ///
    /// The machine is a class rather than a struct on purpose. C# uses a struct in release builds
    /// to save the allocation on the synchronous path, and pays for it with the boxing dance around
    /// MoveNextRunner and a much worse debugging experience. LENS is glue code and is not on
    /// anyone's hot path.
    /// </summary>
    internal abstract class StateMachineBuilder
    {
        #region Constructor

        protected StateMachineBuilder(Context ctx, FunctionNode node, MethodEntity method)
        {
            Ctx = ctx;
            Node = node;
            Method = method;
        }

        #endregion

        #region Fields

        protected readonly Context Ctx;
        protected readonly FunctionNode Node;
        protected readonly MethodEntity Method;

        protected TypeEntity Machine;
        protected TypeSignature MachineSignature;

        /// <summary>
        /// The machine's own copies of the function's type parameters, or null if neither is generic.
        /// </summary>
        private List<GenericParameterEntity> _machineParameters;

        /// <summary>
        /// The pass that flattened the body, which is what knows where its suspension points are.
        /// </summary>
        protected Lowerer Lowering;

        /// <summary>
        /// The field each of the function's arguments is carried in.
        /// </summary>
        private readonly List<Tuple<FunctionArgument, string>> _argumentFields = new List<Tuple<FunctionArgument, string>>();

        /// <summary>
        /// The state of a machine that has been created but not started.
        /// </summary>
        protected const int InitialState = 0;

        /// <summary>
        /// The state of a machine that is either running or done, both of which mean the same thing
        /// to a MoveNext that arrives: there is nowhere to resume to.
        /// </summary>
        protected const int FinishedState = -1;

        #endregion

        #region Build

        /// <summary>
        /// Turns the function into a factory and moves its body into a state machine.
        /// </summary>
        public void Build()
        {
            Validate();

            CreateMachineType();

            var moveNextBody = BuildMoveNextBody();
            var scope = Ctx.ScopeOf(moveNextBody);
            scope.MakeMachineRoot(Machine);

            DeclareArgumentNames(scope);
            DeclareMachineMembers(moveNextBody);

            Method.Body = BuildFactoryBody();
        }

        /// <summary>
        /// Checks everything the rewrite is not prepared to handle, and reads whatever the declared
        /// return type has to say about the machine's shape.
        /// </summary>
        protected abstract void Validate();

        /// <summary>
        /// Declares the fields the protocol needs beyond the state, and the interfaces it demands.
        /// </summary>
        protected abstract void DeclareProtocolFields();

        /// <summary>
        /// Declares MoveNext and whatever else the protocol expects to find on the class.
        /// </summary>
        protected abstract void DeclareMachineMembers(CodeBlockNode moveNextBody);

        /// <summary>
        /// Builds the body of MoveNext: a dispatch that jumps to the suspension point the machine
        /// stopped at, then the lowered original body.
        /// </summary>
        protected abstract CodeBlockNode BuildMoveNextBody();

        /// <summary>
        /// Builds what is left of the function: creating the machine and handing it out.
        /// </summary>
        protected abstract CodeBlockNode BuildFactoryBody();

        #endregion

        #region The machine class

        private void CreateMachineType()
        {
            var name = Ctx.Unique.StateMachineName();

            // a machine built out of a generic function is generic in the same parameters, and
            // under the same names: everything inside the class is spelled as a signature, so a
            // field that said 'T' in the function goes on saying 'T' and resolves to the copy
            _machineParameters = Ctx.CreateGenericParameters(Node.TypeParameters, name);

            MachineSignature = _machineParameters == null
                ? new TypeSignature(name)
                : new TypeSignature(name, Node.TypeParameters.Select(x => new TypeSignature(x.Name)).ToArray());

            Machine = Ctx.CreateType(name, isSealed: true, prepare: false, genericParameters: _machineParameters);
            Machine.Kind = TypeEntityKind.Closure;

            CreateField(EntityNames.StateFieldName, new TypeSignature("int"));

            DeclareProtocolFields();
        }

        /// <summary>
        /// Gives each of the function's arguments a field, and makes that field the name the body
        /// already uses.
        ///
        /// The field names cannot wait for the closure analysis to invent them: the factory is
        /// compiled in the same round as MoveNext and possibly before it, and it has to name the
        /// fields it fills in.
        /// </summary>
        private void DeclareArgumentNames(Scope scope)
        {
            foreach (var arg in Node.Arguments)
            {
                var fieldName = Ctx.Unique.ClosureFieldName(arg.Name);
                var type = ArgumentTypeInMachineSpace(arg);

                // the field is declared by signature where there is one, so that a type naming a
                // parameter of the function resolves to the machine's copy of it
                if (arg.TypeSignature != null && !arg.IsVariadic)
                    CreateField(fieldName, arg.TypeSignature);
                else
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

        /// <summary>
        /// The type of an argument as the machine's own members see it.
        ///
        /// The argument's own resolved type is in the function's terms, and the function's type
        /// parameters mean nothing inside the class: the same signature has to be resolved again,
        /// against the copies the machine declares.
        /// </summary>
        private TypeEntry ArgumentTypeInMachineSpace(FunctionArgument arg)
        {
            if (_machineParameters == null || arg.TypeSignature == null)
                return arg.GetArgumentType(Ctx);

            TypeEntry type = null;
            Ctx.WithGenericScope(_machineParameters, () => type = Ctx.ResolveType(arg.TypeSignature));

            return arg.IsVariadic ? type.MakeArray(Ctx.Resolver) : type;
        }

        #endregion

        #region Shared bodies

        /// <summary>
        /// Lowers the function's body and wraps it in the dispatch that sends a resuming machine
        /// back to where it stopped.
        ///
        /// The dispatch is the lowering pass's answer rather than this class's, because only the
        /// pass knows which suspension points ended up inside a protected region - and a region can
        /// only be entered at its head, never jumped into.
        /// </summary>
        protected CodeBlockNode LowerBody(CodeBlockNode source, LabelRef doneLabel)
        {
            var body = Lowering.Lower(source, false);

            var dispatch = Lowering.RootDispatch();
            dispatch.Add(new GotoNode(doneLabel, Expr.NotEqual(GetState(), Expr.Int(InitialState))));

            // the machine is running from here on: should the body throw, a further MoveNext must
            // find a finished machine rather than resume into the statement that failed
            dispatch.Add(SetState(Expr.Int(FinishedState)));

            body.Statements.InsertRange(0, dispatch);
            return body;
        }

        /// <summary>
        ///     var m = new machine ()
        ///     m.arg = [the argument, or this.arg]
        ///     m
        /// </summary>
        protected CodeBlockNode BuildFreshMachineBody(bool fromThis, Func<string, IEnumerable<NodeBase>> extraSetup = null, Func<string, NodeBase> result = null)
        {
            var name = Ctx.Unique.TempVariableName();
            var block = new CodeBlockNode(ScopeKind.FunctionRoot);

            block.Add(Expr.Var(name, Expr.New(MachineSignature)));

            foreach (var curr in _argumentFields)
            {
                var value = fromThis
                    ? Expr.GetMember(Expr.This(), curr.Item2)
                    : Expr.Get(curr.Item1.Name) as NodeBase;

                block.Add(Expr.SetMember(Expr.Get(name), curr.Item2, value));
            }

            if (extraSetup != null)
                foreach (var curr in extraSetup(name))
                    block.Add(curr);

            block.Add(result != null ? result(name) : Expr.Get(name));
            return block;
        }

        #endregion

        #region Helpers

        protected static NodeBase GetState()
        {
            return Expr.GetMember(Expr.This(), EntityNames.StateFieldName);
        }

        protected static NodeBase SetState(NodeBase value)
        {
            return Expr.SetMember(Expr.This(), EntityNames.StateFieldName, value);
        }

        protected static CodeBlockNode Block(params NodeBase[] statements)
        {
            var block = new CodeBlockNode(ScopeKind.FunctionRoot);
            block.AddRange(statements);
            return block;
        }

        protected void CreateField(string name, TypeSignature type)
        {
            Machine.CreateField(name, type, prepare: false).Kind = TypeContentsKind.Closure;
        }

        protected void CreateField(string name, TypeEntry type)
        {
            Machine.CreateField(name, type, prepare: false).Kind = TypeContentsKind.Closure;
        }

        protected void CreateMethod(string name, TypeSignature returnType, CodeBlockNode body, MethodInfo explicitOverride = null)
        {
            var method = Machine.CreateMethod(name, returnType, (IEnumerable<FunctionArgument>) null, isStatic: false, isVirtual: true, prepare: false);
            method.Kind = TypeContentsKind.AutoGenerated;
            method.Body = body;
            method.ExplicitOverride = explicitOverride;
        }

        /// <summary>
        /// Rejects everything both front-ends reject for the same reason.
        /// </summary>
        protected void ValidateCommon()
        {
            // a resume point inside a lambda would be the lambda's own, and a lambda has no way to
            // declare that it drives a machine
            Lowerer.CheckNoNestedResumePoints(Node.Body);

            foreach (var arg in Node.Arguments)
                if (arg.IsRefArgument)
                    Error(CompilerMessages.ClosureRef, arg.Name);
        }

        [ContractAnnotation("=> halt")]
        protected static void Error(string message, params object[] args)
        {
            throw new LensCompilerException(string.Format(message, args));
        }

        #endregion
    }
}
