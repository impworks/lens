using System.Collections.Generic;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.Internals;
using Lens.SyntaxTree.Literals;
using Lens.SyntaxTree.Operators;
using Lens.SyntaxTree.Operators.Binary;
using Lens.SyntaxTree.Operators.TypeBased;
using Lens.SyntaxTree.PatternMatching;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// Lifts the awaits out of an expression, so that each of them becomes a statement of its own.
    ///
    /// A resume point has to be a statement: it is a place the machine leaves from and comes back
    /// to, and whatever a half-evaluated expression had left on the evaluation stack is not there
    /// when it returns. An await written in the middle of an expression is therefore rewritten into
    /// one written before it, and the expression is rebuilt around the name that holds the result.
    ///
    /// What makes this more than moving a subexpression is order. Everything the expression would
    /// have evaluated before reaching the await has to be evaluated before it still, so it is
    /// evaluated into a temporary on the way past:
    ///
    ///     f (g ()) (await t) (h ())
    ///
    ///     var a = g ()                # would have run before the suspension, so it still does
    ///     [suspend]
    ///     f a [result] (h ())         # h was always going to run after, and still does
    ///
    /// Only what precedes the last await needs a temporary. Anything after it already ran after the
    /// suspension in the source, and rebuilding leaves it exactly where it was.
    /// </summary>
    internal partial class Lowerer
    {
        #region Spilling

        /// <summary>
        /// Rewrites an expression into one that no longer contains an await, appending the
        /// statements that have to run before it - the suspensions, and the evaluations that were
        /// ordered ahead of them.
        /// </summary>
        private NodeBase Spill(NodeBase expr, List<NodeBase> output)
        {
            if (expr == null || !ContainsResumePoint(expr))
                return expr;

            switch (expr)
            {
                case AwaitNode node:
                    return LowerAwait(node, output);

                // a construct that branches cannot leave its value on the evaluation stack, because
                // what one branch leaves there is not there when another arrives at the same label.
                // It is flattened like any other, and the value goes into a name instead
                case IfNode node:
                    return SpillIf(node, output);

                case MatchNode node:
                    return SpillMatch(node, output);

                case BooleanOperatorNode node:
                    return SpillShortCircuit(node, output);

                case CoalesceOperatorNode node:
                    return SpillCoalesce(node, output);

                // a null-safe chain skips the rest of itself when a receiver turns out to be null,
                // and produces the default of its own type when it does - a type that only binding
                // knows, and one that is not the type of the chain's value but that type lifted so
                // that it can also be null. Nothing this pass could write down stands for it, and a
                // name holding the value alone would answer 0 where the chain answers null
                case NullSafeChainNode node:
                    Error(node, CompilerMessages.ResumePointInNullSafeChain);
                    return null;
            }

            var operands = expr.Operands;
            var last = LastSuspendingOperand(operands);

            // the await is somewhere this node does not report as an operand, which means the node
            // either evaluates it conditionally or is not an expression at all
            if (last < 0)
                Error(expr, CompilerMessages.AwaitPosition);

            var rebuilt = new NodeBase[operands.Count];
            for (var idx = 0; idx < operands.Count; idx++)
            {
                var spilled = Spill(operands[idx], output);

                rebuilt[idx] = idx < last && expr.CanHoistOperand(idx)
                    ? Hoist(spilled, output)
                    : spilled;
            }

            var result = expr.WithOperands(rebuilt);
            CopyLocation(expr, result);
            return result;
        }

        /// <summary>
        /// Suspends the machine until the operation finishes, and returns the expression that reads
        /// its result. Reading it is not optional even when nobody wants the value: that is where a
        /// failed operation turns back into an exception.
        /// </summary>
        private NodeBase LowerAwait(AwaitNode node, List<NodeBase> output)
        {
            if (_emitAwait == null)
                Error(node, CompilerMessages.AwaitNotInAsync);

            var awaited = Spill(node.Expression, output);

            NodeBase result = null;
            Suspend(point => result = _emitAwait(awaited, point, output), output);
            return result;
        }

        /// <summary>
        /// Evaluates an expression now and hands back the name that holds it, so that a suspension
        /// later in the same expression cannot move it after the suspension.
        /// </summary>
        private NodeBase Hoist(NodeBase expr, List<NodeBase> output)
        {
            if (IsStable(expr))
                return expr;

            var name = _ctx.Unique.TempVariableName();
            var declaration = Expr.Var(name, expr);
            CopyLocation(expr, declaration);
            output.Add(declaration);

            var read = Expr.Get(name);
            CopyLocation(expr, read);
            return read;
        }

        #endregion

        #region Constructs that branch

        /// <summary>
        ///     var result
        ///     goto else unless cond
        ///     result = [true branch]
        ///     goto end
        /// else:
        ///     result = [false branch]
        /// end:
        ///     result
        /// </summary>
        private NodeBase SpillIf(IfNode node, List<NodeBase> output)
        {
            var result = DeferredName(output);
            var elseLabel = NewLabel("else");
            var endLabel = NewLabel("endif");

            output.Add(new GotoNode(elseLabel, Spill(node.Condition, output), false));
            output.Add(LowerBlock(AssignTail(node.TrueAction, result), false));
            output.Add(new GotoNode(endLabel));
            output.Add(new LabelNode(elseLabel));

            // an 'if' without an else that is nonetheless being read leaves the name at its default,
            // which is what the node itself produces for the branch that is not there
            if (node.FalseAction != null)
                output.Add(LowerBlock(AssignTail(node.FalseAction, result), false));

            output.Add(new LabelNode(endLabel));

            return Read(result, node);
        }

        /// <summary>
        /// A match whose value is wanted: the same flattening a match in statement position gets,
        /// with each case body's value going into a name rather than onto the stack.
        /// </summary>
        private NodeBase SpillMatch(MatchNode node, List<NodeBase> output)
        {
            var result = DeferredName(output);
            LowerMatch(node, output, result);
            return Read(result, node);
        }

        /// <summary>
        /// The right operand of '&amp;&amp;' or '||' is evaluated only when the left one did not
        /// already decide the answer, so a suspension in it has to sit behind the same jump.
        ///
        ///     var left = [left]
        ///     var right
        ///     goto end unless left            # '||' jumps when it is set instead
        ///     right = [right]
        /// end:
        ///     left &amp;&amp; right
        ///
        /// The operator is left standing over the two names, rather than replaced by the answer it
        /// would have given, so that it goes on deciding what the operands mean and what the whole
        /// thing produces. It short-circuits a second time over names that are already in hand,
        /// which costs a branch and settles nothing that was not already settled - and when it does
        /// skip the right-hand name, that name is holding the default it was never assigned, which
        /// is the answer the operator was going to give anyway.
        /// </summary>
        private NodeBase SpillShortCircuit(BooleanOperatorNode node, List<NodeBase> output)
        {
            var left = _ctx.Unique.TempVariableName();
            output.Add(Expr.Var(left, Spill(node.LeftOperand, output)));

            var right = DeferredName(output);
            var endLabel = NewLabel("shortcircuit");

            // 'and' has nothing left to ask once the left operand is false, 'or' once it is true
            output.Add(new GotoNode(endLabel, Expr.Get(left), node.Kind == LogicalOperatorKind.Or));
            output.Add(Expr.Set(right, Spill(node.RightOperand, output)));
            output.Add(new LabelNode(endLabel));

            return Rebuilt(node, Expr.Get(left), Expr.Get(right));
        }

        /// <summary>
        /// The fallback of '??' is evaluated only when the left operand turned out to be null, and
        /// is lifted out behind the same check - the operator itself staying to decide the result,
        /// for the reason given above.
        ///
        ///     var left = [left]
        ///     var right
        ///     goto end unless left == null
        ///     right = [right]
        /// end:
        ///     left ?? right
        /// </summary>
        private NodeBase SpillCoalesce(CoalesceOperatorNode node, List<NodeBase> output)
        {
            var left = _ctx.Unique.TempVariableName();
            output.Add(Expr.Var(left, Spill(node.LeftOperand, output)));

            var right = DeferredName(output);
            var endLabel = NewLabel("coalesce");

            output.Add(new GotoNode(endLabel, Expr.Equal(Expr.Get(left), Expr.Null()), false));
            output.Add(Expr.Set(right, Spill(node.RightOperand, output)));
            output.Add(new LabelNode(endLabel));

            return Rebuilt(node, Expr.Get(left), Expr.Get(right));
        }

        /// <summary>
        /// The operator again, over the names its operands were evaluated into.
        /// </summary>
        private static NodeBase Rebuilt(NodeBase node, NodeBase left, NodeBase right)
        {
            NodeBase result;

            if (node is CoalesceOperatorNode)
                result = Expr.Coalesce(left, right);
            else
                result = ((BinaryOperatorNodeBase) node).WithOperands(new[] {left, right});

            CopyLocation(node, result);
            return result;
        }

        /// <summary>
        /// Declares the name that will hold what a branching construct produced.
        /// </summary>
        private string DeferredName(List<NodeBase> output)
        {
            var name = _ctx.Unique.TempVariableName();
            output.Add(new DeferredNameNode(name));
            return name;
        }

        private static NodeBase Read(string name, NodeBase origin)
        {
            var result = Expr.Get(name);
            CopyLocation(origin, result);
            return result;
        }

        /// <summary>
        /// Rewrites a block so that its value is assigned to a name instead of being produced.
        /// </summary>
        private static CodeBlockNode AssignTail(CodeBlockNode body, string name)
        {
            var lastIdx = body.Statements.FindLastIndex(x => !(x is IMetaNode));

            var result = new CodeBlockNode(body.ScopeKind);
            for (var idx = 0; idx < body.Statements.Count; idx++)
            {
                result.Add(
                    idx == lastIdx
                        ? AssignValue(body.Statements[idx], name)
                        : body.Statements[idx]
                );
            }

            CopyLocation(body, result);
            return result;
        }

        /// <summary>
        /// Assigns what an expression produces to a name.
        /// </summary>
        private static NodeBase AssignValue(NodeBase value, string name)
        {
            // a branch that throws never gets as far as having a value to hand over
            if (value is ThrowNode)
                return value;

            var result = Expr.Set(name, value);
            CopyLocation(value, result);
            return result;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The position of the last operand that suspends, or -1 if none of them does.
        /// </summary>
        private static int LastSuspendingOperand(IReadOnlyList<NodeBase> operands)
        {
            for (var idx = operands.Count - 1; idx >= 0; idx--)
            {
                if (ContainsResumePoint(operands[idx]))
                    return idx;
            }

            return -1;
        }

        /// <summary>
        /// Whether evaluating an expression can be left where it is, because when it happens makes
        /// no difference to what it produces and evaluating it has no effect of its own.
        /// </summary>
        private static bool IsStable(NodeBase expr)
        {
            return expr is ILiteralNode
                   || expr is UnitNode
                   || expr is TypeofOperatorNode
                   || expr is DefaultOperatorNode;
        }

        #endregion
    }
}
