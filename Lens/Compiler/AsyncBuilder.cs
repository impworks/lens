using System.Collections.Generic;
using System.Linq;
using Lens.Compiler.Entities;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Internals;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// Rewrites a function that awaits into a state machine driven by a task.
    ///
    /// This is the same machine an iterator gets - the same class, the same hoisting, the same
    /// numbered dispatch - with a different protocol bolted on. The awaited operation is matched
    /// structurally rather than against Task in particular: anything with GetAwaiter, IsCompleted,
    /// OnCompleted and GetResult will do, so ValueTask, Task.Yield's awaitable and a host's own
    /// awaitable all work without the compiler knowing they exist.
    ///
    /// The result is delivered through a TaskCompletionSource rather than an
    /// AsyncTaskMethodBuilder. The builder API is designed to be driven by a struct machine passed
    /// by reference through generic methods with constraints, which is a lot of ceremony to buy an
    /// allocation LENS does not care about; a completion source expresses the same thing in code
    /// the compiler can already emit.
    /// </summary>
    internal class AsyncBuilder : StateMachineBuilder
    {
        #region Constructor

        public AsyncBuilder(Context ctx, FunctionNode node, MethodEntity method) : base(ctx, node, method)
        {
        }

        #endregion

        #region Fields

        /// <summary>
        /// The type the task carries. A function that returns a bare Task still needs one, because
        /// a completion source has to be told something - it gets object, and null.
        /// </summary>
        private TypeSignature _resultSignature;

        /// <summary>
        /// Whether the function produces a value at all, or only completion.
        /// </summary>
        private bool _hasResult;

        #endregion

        #region Validation

        /// <summary>
        /// Checks whether a function suspends itself waiting for something.
        ///
        /// There is no 'async' marker. C# needs one because 'await' had to keep working as an
        /// identifier; LENS has no such history, so the presence of the keyword is the answer. The
        /// return type is still declared rather than inferred, so nothing about the signature
        /// depends on the body.
        /// </summary>
        public static bool IsAsync(FunctionNode node)
        {
            return Lowerer.ContainsAnywhere<AwaitNode>(node.Body);
        }

        protected override void Validate()
        {
            ValidateCommon();

            if (Node.IsPure)
                Error(CompilerMessages.AsyncPure, Node.Name);

            var returnType = Node.ReturnTypeSignature;
            if (returnType == null || string.IsNullOrEmpty(returnType.FullSignature))
                Error(CompilerMessages.AsyncReturnTypeRequired, Node.Name);

            if (!IsTaskSignature(returnType))
                Error(CompilerMessages.AsyncReturnTypeMismatch, Node.Name, returnType.FullSignature);

            // async void is not supported, and deliberately: it is a footgun whose only purpose is
            // an event handler signature, and glue code has no need for one
            _hasResult = returnType.Arguments != null && returnType.Arguments.Length == 1;
            _resultSignature = _hasResult ? returnType.Arguments[0] : new TypeSignature("object");
        }

        private static bool IsTaskSignature(TypeSignature signature)
        {
            if (signature.Postfix != null)
                return false;

            var name = signature.Name;
            if (name != "Task" && !name.EndsWith(".Task"))
                return false;

            return signature.Arguments == null || signature.Arguments.Length == 1;
        }

        #endregion

        #region The machine class

        protected override void DeclareProtocolFields()
        {
            CreateField(EntityNames.CompletionFieldName, CompletionSource(_resultSignature));
        }

        protected override void DeclareMachineMembers(CodeBlockNode moveNextBody)
        {
            CreateMethod("MoveNext", new TypeSignature("Void"), moveNextBody);

            // MoveNext cannot catch its own exceptions: it is resumed into, and nothing may be
            // resumed into a protected region. So the try lives one method out, in the only place
            // MoveNext is ever called from
            var error = Ctx.Unique.TempVariableName();
            CreateMethod(
                EntityNames.ResumeMethodName,
                new TypeSignature("Void"),
                Block(
                    Expr.Try(
                        Expr.Block(Expr.Invoke(Expr.This(), "MoveNext")),
                        Expr.Catch(
                            "System.Exception",
                            error,
                            Expr.Block(Expr.Invoke(Completion(), "TrySetException", Expr.Get(error)))
                        )
                    )
                )
            );
        }

        #endregion

        #region Method bodies

        /// <summary>
        ///     [dispatch]
        ///     [the lowered body, ending in the completion source being told the answer]
        /// done:
        ///     state = -1
        /// suspend:
        /// </summary>
        protected override CodeBlockNode BuildMoveNextBody()
        {
            Lowering = new Lowerer(Ctx, emitAwait: EmitAwait);

            var doneLabel = new LabelRef("done");
            var body = LowerBody(WithResultHandover(), doneLabel);

            body.Add(new LabelNode(doneLabel));
            body.Add(SetState(Expr.Int(FinishedState)));

            // every suspension leaves rather than returns, because a return is not valid inside a
            // protected region; this is where the leaving lands, and the method ends anyway
            body.Add(new LabelNode(Lowering.SuspendLabel));

            return body;
        }

        /// <summary>
        /// Rewrites the body so that its value ends up in the completion source.
        ///
        /// A LENS function has no return statement: its value is the value of its last statement.
        /// That statement is therefore the one that has to hand the answer over - unless it is
        /// itself an await, in which case the await is lowered first and its result handed over
        /// afterwards, which is what makes 'fun fetch:Task&lt;string&gt; -> await (...)' work.
        /// </summary>
        private CodeBlockNode WithResultHandover()
        {
            var statements = new List<NodeBase>(Node.Body.Statements);

            if (!_hasResult)
            {
                statements.Add(SetResult(Expr.Null()));
            }
            else
            {
                var lastIdx = statements.FindLastIndex(x => !(x is IMetaNode));
                if (lastIdx < 0)
                    Error(CompilerMessages.AsyncReturnTypeMismatch, Node.Name, Node.ReturnTypeSignature.FullSignature);

                var last = statements[lastIdx];

                // a body that ends by throwing never completes normally, and there is nothing to
                // hand over: the exception reaches the task through Resume instead
                if (last is ThrowNode)
                    return Node.Body;

                statements.RemoveAt(lastIdx);

                if (last is AwaitNode)
                {
                    var name = Ctx.Unique.TempVariableName();
                    statements.Add(Expr.Var(name, last));
                    statements.Add(SetResult(Expr.Get(name)));
                }
                else
                {
                    statements.Add(SetResult(last));
                }
            }

            var body = new CodeBlockNode(Node.Body.ScopeKind);
            body.AddRange(statements);
            return body;
        }

        /// <summary>
        ///     var a = (operation).GetAwaiter ()
        ///     goto ready if a.IsCompleted
        ///     state = k
        ///     a.OnCompleted (this.Resume)
        ///     leave suspend
        /// resume_k:
        ///     state = -1
        /// ready:
        ///     [the caller appends whatever consumes a.GetResult ()]
        ///
        /// The awaiter is an ordinary local of MoveNext, which means the hoisting that puts every
        /// other name into a machine field puts it there too - without which it would not survive
        /// the return.
        /// </summary>
        private NodeBase EmitAwait(NodeBase awaited, ResumePoint point, List<NodeBase> output)
        {
            var awaiter = Ctx.Unique.TempVariableName();
            var readyLabel = new LabelRef("ready_" + point.State);

            output.Add(Expr.Var(awaiter, Expr.Invoke(awaited, "GetAwaiter")));
            output.Add(new GotoNode(readyLabel, Expr.GetMember(Expr.Get(awaiter), "IsCompleted")));
            output.Add(SetState(Expr.Int(point.State)));
            output.Add(Expr.Invoke(Expr.Get(awaiter), "OnCompleted", ResumeCallback()));
            output.Add(new GotoNode(Lowering.SuspendLabel, isLeave: true));
            output.Add(new LabelNode(point.Label));
            output.Add(SetState(Expr.Int(FinishedState)));
            output.Add(new LabelNode(readyLabel));

            return Expr.Invoke(Expr.Get(awaiter), "GetResult");
        }

        /// <summary>
        ///     var m = new machine ()
        ///     m.arg = arg
        ///     m.completion = new TaskCompletionSource ()
        ///     m.Resume ()
        ///     m.completion.Task
        ///
        /// The first step runs synchronously, on the caller's thread, exactly as C# does it: an
        /// async function that never actually suspends never leaves its caller.
        /// </summary>
        protected override CodeBlockNode BuildFactoryBody()
        {
            return BuildFreshMachineBody(
                fromThis: false,
                extraSetup: name => new NodeBase[]
                {
                    Expr.SetMember(Expr.Get(name), EntityNames.CompletionFieldName, Expr.New(CompletionSource(_resultSignature))),
                    Expr.Invoke(Expr.Get(name), EntityNames.ResumeMethodName)
                },
                result: name => Expr.GetMember(Expr.GetMember(Expr.Get(name), EntityNames.CompletionFieldName), "Task")
            );
        }

        #endregion

        #region Helpers

        private static TypeSignature CompletionSource(TypeSignature result)
        {
            return new TypeSignature("System.Threading.Tasks.TaskCompletionSource", result);
        }

        private static NodeBase Completion()
        {
            return Expr.GetMember(Expr.This(), EntityNames.CompletionFieldName);
        }

        private static NodeBase SetResult(NodeBase value)
        {
            return Expr.Invoke(Completion(), "SetResult", value);
        }

        /// <summary>
        /// The continuation the awaiter calls, as a delegate over the machine's own Resume.
        /// </summary>
        private static NodeBase ResumeCallback()
        {
            return new SelfMethodDelegateNode(EntityNames.ResumeMethodName);
        }

        #endregion
    }
}
