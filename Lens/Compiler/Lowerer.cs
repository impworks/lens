using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Internals;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// Rewrites structured control flow into a flat list of statements over an explicit label and
    /// jump vocabulary.
    ///
    /// LENS compiles the syntax tree straight to IL: a while node emits its own loop, an if node
    /// emits its own branches. A state machine needs the opposite shape, because a resume point has
    /// to be a jump target and nothing can jump into the middle of a node that emits itself.
    ///
    /// The pass rewrites rather than mutates: a node that has to be flattened is replaced by the
    /// statements that flatten it, and every node that does not is reused as it stands. Nothing the
    /// parser produced is written to.
    ///
    /// Blocks are kept nested. Only the control flow between them is flattened, so a variable
    /// declared in a loop body still belongs to that body's frame - the pass never has to merge two
    /// frames, and therefore never has to rename anything.
    /// </summary>
    internal class Lowerer
    {
        #region Constructor

        public Lowerer(Context ctx, Action<NodeBase, List<NodeBase>> emitYield = null, bool lowerEverything = false)
        {
            _ctx = ctx;
            _emitYield = emitYield;
            _lowerEverything = lowerEverything;
        }

        #endregion

        #region Fields

        private readonly Context _ctx;

        /// <summary>
        /// Appends the statements that hand one value over to the consumer of an iterator.
        /// Null when the pass is being used without a state machine, which is how it is validated.
        /// </summary>
        private readonly Action<NodeBase, List<NodeBase>> _emitYield;

        /// <summary>
        /// Whether every construct is to be flattened, rather than only the ones that contain a
        /// resume point. Only a test asks for this: it is how the pass is checked to preserve
        /// behaviour on code that has nothing to do with state machines.
        /// </summary>
        private readonly bool _lowerEverything;

        private int _labelId;

        #endregion

        #region Entry point

        /// <summary>
        /// Rewrites a method body. The body is a statement list, so nothing in it is in value
        /// position except its own last statement.
        /// </summary>
        public CodeBlockNode Lower(CodeBlockNode body, bool valuePosition)
        {
            return LowerBlock(body, valuePosition);
        }

        #endregion

        #region Blocks and statements

        private CodeBlockNode LowerBlock(CodeBlockNode block, bool valuePosition)
        {
            var lastIdx = block.Statements.FindLastIndex(x => !(x is IMetaNode));

            var output = new List<NodeBase>();
            for (var idx = 0; idx < block.Statements.Count; idx++)
                LowerStatement(block.Statements[idx], valuePosition && idx == lastIdx, output);

            var result = new CodeBlockNode(block.ScopeKind);
            result.AddRange(output);
            CopyLocation(block, result);
            return result;
        }

        private void LowerStatement(NodeBase stmt, bool valuePosition, List<NodeBase> output)
        {
            if (stmt is YieldNode yield)
            {
                LowerYield(yield, output);
                return;
            }

            var hasYield = ContainsYield(stmt);
            if (!hasYield && !_lowerEverything)
            {
                output.Add(stmt);
                return;
            }

            // the value of a lowered construct would have to survive an arbitrary jump, and there
            // is nowhere for it to wait: what a flattened branch leaves on the stack is not there
            // when another branch arrives at the same label
            if (valuePosition)
            {
                if (hasYield)
                    Error(stmt, CompilerMessages.YieldInProtectedBlock);

                output.Add(stmt);
                return;
            }

            switch (stmt)
            {
                case CodeBlockNode block:
                    output.Add(LowerBlock(block, false));
                    return;

                case IfNode node:
                    LowerIf(node, output);
                    return;

                case WhileNode node:
                    LowerWhile(node, output);
                    return;

                case ForeachNode node:
                    LowerForeach(node, output);
                    return;
            }

            // try, using and match all open a protected region or a construct with its own labels,
            // and neither can be resumed into
            if (hasYield)
                Error(stmt, CompilerMessages.YieldInProtectedBlock);

            output.Add(stmt);
        }

        #endregion

        #region Control structures

        /// <summary>
        ///     goto else unless cond
        ///     [true branch]
        ///     goto end
        /// else:
        ///     [false branch]
        /// end:
        /// </summary>
        private void LowerIf(IfNode node, List<NodeBase> output)
        {
            var elseLabel = NewLabel("else");

            output.Add(new GotoNode(elseLabel, node.Condition, false));
            output.Add(LowerBlock(node.TrueAction, false));

            if (node.FalseAction == null)
            {
                output.Add(new LabelNode(elseLabel));
                return;
            }

            var endLabel = NewLabel("endif");
            output.Add(new GotoNode(endLabel));
            output.Add(new LabelNode(elseLabel));
            output.Add(LowerBlock(node.FalseAction, false));
            output.Add(new LabelNode(endLabel));
        }

        /// <summary>
        /// begin:
        ///     goto end unless cond
        ///     [body]
        ///     goto begin
        /// end:
        /// </summary>
        private void LowerWhile(WhileNode node, List<NodeBase> output)
        {
            var beginLabel = NewLabel("while");
            var endLabel = NewLabel("endwhile");

            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, node.Condition, false));
            output.Add(LowerBlock(node.Body, false));
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));
        }

        private void LowerForeach(ForeachNode node, List<NodeBase> output)
        {
            if (node.IterableExpression != null)
                LowerForeachOverSequence(node, output);
            else
                LowerForeachOverRange(node, output);
        }

        /// <summary>
        ///     var e = enumerator(seq)
        /// begin:
        ///     goto end unless e.MoveNext()
        ///     { let x = e.Current; [body] }
        ///     goto begin
        /// end:
        ///     dispose e
        /// </summary>
        private void LowerForeachOverSequence(ForeachNode node, List<NodeBase> output)
        {
            var beginLabel = NewLabel("for");
            var endLabel = NewLabel("endfor");
            var iterator = _ctx.Unique.TempVariableName();

            output.Add(Expr.Var(iterator, new GetEnumeratorNode(node.IterableExpression)));
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Expr.Invoke(Expr.Get(iterator), "MoveNext"), false));
            output.Add(
                LoopBody(
                    node,
                    Expr.GetMember(Expr.Get(iterator), "Current")
                )
            );
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));

            // there is no try/finally here, and so no disposal on an abrupt exit. A state machine
            // cannot resume into a protected region, and making one work is the whole of the
            // try/finally story this phase deliberately left for later.
            output.Add(new DisposeNode(Expr.Get(iterator)));
        }

        /// <summary>
        ///     var i = from
        ///     var step = sign(to - i)
        /// begin:
        ///     goto end if i == to
        ///     { let x = i; [body] }
        ///     i = i + step
        ///     goto begin
        /// end:
        /// </summary>
        private void LowerForeachOverRange(ForeachNode node, List<NodeBase> output)
        {
            var beginLabel = NewLabel("for");
            var endLabel = NewLabel("endfor");
            var index = _ctx.Unique.TempVariableName();
            var step = _ctx.Unique.TempVariableName();

            output.Add(Expr.Var(index, node.RangeStart));
            output.Add(Expr.Var(step, Expr.Invoke("Math", "Sign", Expr.Sub(node.RangeEnd, Expr.Get(index)))));
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Expr.Equal(Expr.Get(index), node.RangeEnd)));
            output.Add(LoopBody(node, Expr.Get(index)));
            output.Add(Expr.Set(index, Expr.Add(Expr.Get(index), Expr.Get(step))));
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));
        }

        /// <summary>
        /// Wraps a loop body in the frame that holds its iteration variable, exactly as the node's
        /// own expansion does.
        /// </summary>
        private CodeBlockNode LoopBody(ForeachNode node, NodeBase itemGetter)
        {
            var assignment = node.Local == null
                ? Expr.Let(node.VariableName, itemGetter)
                : Expr.Set(node.Local, itemGetter) as NodeBase;

            // the frame is a loop frame, exactly as the node's own expansion makes it: a name
            // declared in a loop is a fresh name on every iteration, and a lambda that captures it
            // must capture that iteration's one
            var wrapper = new CodeBlockNode(ScopeKind.Loop);
            wrapper.Add(assignment);
            wrapper.Add(LowerBlock(node.Body, false));
            CopyLocation(node, wrapper);
            return wrapper;
        }

        #endregion

        #region Resume points

        private void LowerYield(YieldNode node, List<NodeBase> output)
        {
            if (_emitYield == null)
                Error(node, CompilerMessages.YieldNotInIterator);

            if (!node.IsSequence)
            {
                _emitYield(node.Expression, output);
                return;
            }

            // 'yield from' is a loop that yields each item in turn: once the machine exists it costs
            // nothing beyond the loop, and it is how iterators are composed
            var beginLabel = NewLabel("yieldfrom");
            var endLabel = NewLabel("endyieldfrom");
            var iterator = _ctx.Unique.TempVariableName();

            output.Add(Expr.Var(iterator, new GetEnumeratorNode(node.Expression)));
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Expr.Invoke(Expr.Get(iterator), "MoveNext"), false));
            _emitYield(Expr.GetMember(Expr.Get(iterator), "Current"), output);
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));
            output.Add(new DisposeNode(Expr.Get(iterator)));
        }

        #endregion

        #region Helpers

        private static void CopyLocation(LocationEntity from, LocationEntity to)
        {
            to.StartLocation = from.StartLocation;
            to.EndLocation = from.EndLocation;
        }

        private LabelRef NewLabel(string kind)
        {
            return new LabelRef($"{kind}_{++_labelId}");
        }

        private static void Error(NodeBase node, string message, params object[] args)
        {
            throw new LensCompilerException(string.Format(message, args), node);
        }

        /// <summary>
        /// Checks whether a subtree hands values to the consumer of the function currently being
        /// rewritten. A lambda's yields are its own, not this function's, and are rejected
        /// separately.
        /// </summary>
        public static bool ContainsYield(NodeBase node)
        {
            if (node == null || node is FunctionNodeBase)
                return false;

            if (node is YieldNode)
                return true;

            return node.GetChildren().Any(child => ContainsYield(child?.Node));
        }

        /// <summary>
        /// Reports a yield that appears where no state machine can consume it.
        /// </summary>
        public static void CheckNoNestedYields(NodeBase node)
        {
            if (node == null)
                return;

            if (node is FunctionNodeBase fn)
            {
                if (ContainsYieldAnywhere(fn.Body))
                    Error(node, CompilerMessages.YieldInLambda);

                return;
            }

            foreach (var child in node.GetChildren())
                CheckNoNestedYields(child?.Node);
        }

        private static bool ContainsYieldAnywhere(NodeBase node)
        {
            if (node == null)
                return false;

            if (node is YieldNode)
                return true;

            return node.GetChildren().Any(child => ContainsYieldAnywhere(child?.Node));
        }

        #endregion
    }
}
