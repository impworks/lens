using System.Collections;
using System.Collections.Generic;
using Lens.Compiler.Entities;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Internals;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// Rewrites a function that yields into a class that implements the iterator protocol.
    ///
    /// The function keeps its name and signature and becomes a factory: it creates the machine,
    /// hands it the arguments and returns it. Everything the function used to do moves into
    /// MoveNext, whose body is the lowered original with a dispatch switch in front of it.
    /// </summary>
    internal class IteratorBuilder : StateMachineBuilder
    {
        #region Constructor

        public IteratorBuilder(Context ctx, FunctionNode node, MethodEntity method) : base(ctx, node, method)
        {
        }

        #endregion

        #region Fields

        private TypeSignature _elementSignature;

        #endregion

        #region Validation

        /// <summary>
        /// Checks whether a function hands values to a consumer rather than returning one.
        /// </summary>
        public static bool IsIterator(FunctionNode node)
        {
            return Lowerer.ContainsAnywhere<YieldNode>(node.Body);
        }

        protected override void Validate()
        {
            ValidateCommon();

            if (Node.IsPure)
                Error(CompilerMessages.IteratorPure, Node.Name);

            if (Node.TypeParameters != null && Node.TypeParameters.Count > 0)
                Error(CompilerMessages.IteratorGeneric, Node.Name);

            var returnType = Node.ReturnTypeSignature;
            if (returnType == null || string.IsNullOrEmpty(returnType.FullSignature))
                Error(CompilerMessages.IteratorReturnTypeRequired, Node.Name);

            _elementSignature = GetElementSignature(returnType);
            if (_elementSignature == null)
                Error(CompilerMessages.IteratorReturnTypeMismatch, Node.Name, returnType.FullSignature);
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

        protected override void DeclareProtocolFields()
        {
            Machine.InterfaceSignatures = new[]
            {
                Sequence(_elementSignature),
                Enumerator(_elementSignature),
                new TypeSignature("System.Collections.IEnumerable"),
                new TypeSignature("System.Collections.IEnumerator"),
                new TypeSignature("System.IDisposable")
            };

            CreateField(EntityNames.CurrentFieldName, _elementSignature);
        }

        protected override void DeclareMachineMembers(CodeBlockNode moveNextBody)
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
        ///     [dispatch]
        ///     [the lowered body, whose yields jump back into it]
        /// done:
        ///     state = -1
        ///     false
        /// </summary>
        protected override CodeBlockNode BuildMoveNextBody()
        {
            var body = new Lowerer(Ctx, EmitYield).Lower(Node.Body, false);
            var doneLabel = new LabelRef("done");

            body.Statements.InsertRange(0, BuildDispatch(doneLabel));
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
            var label = NewResumePoint(out var state);

            output.Add(Expr.SetMember(Expr.This(), EntityNames.CurrentFieldName, value));
            output.Add(SetState(Expr.Int(state)));
            output.Add(new ReturnValueNode(Expr.True()));
            output.Add(new LabelNode(label));
            output.Add(SetState(Expr.Int(FinishedState)));
        }

        /// <summary>
        /// The body of the function itself, which now only builds a machine and hands it over.
        ///
        /// GetEnumerator hands out a fresh machine every time rather than returning itself on the
        /// first call. C# checks the thread id and reuses the instance to save an allocation; that
        /// saves one allocation per iteration of an already-allocating protocol and costs a subtle
        /// piece of state that has to be right.
        /// </summary>
        protected override CodeBlockNode BuildFactoryBody()
        {
            return BuildFreshMachineBody(fromThis: false);
        }

        #endregion

        #region Helpers

        private static TypeSignature Sequence(TypeSignature element)
        {
            return new TypeSignature("System.Collections.Generic.IEnumerable", element);
        }

        private static TypeSignature Enumerator(TypeSignature element)
        {
            return new TypeSignature("System.Collections.Generic.IEnumerator", element);
        }

        #endregion
    }
}
