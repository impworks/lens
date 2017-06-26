using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Parser
{
    internal partial class LensParser
    {
        #region Error reporting

        /// <summary>
        /// Throws an error bound to current lexem.
        /// </summary>
        [ContractAnnotation(" => halt")]
        [DebuggerStepThrough]
        private void Error(string msg, params object[] args)
        {
            throw new LensCompilerException(
                string.Format(msg, args),
                _lexems[_lexemId]
            );
        }

        #endregion

        #region Lexem handling

        /// <summary>
        /// Checks if the pattern at current location matches given one.
        /// </summary>
        [DebuggerStepThrough]
        private bool Peek(params LexemType[] types)
        {
            return Peek(0, types);
        }

        /// <summary>
        /// Checks if the pattern at offset matches given one.
        /// </summary>
        [DebuggerStepThrough]
        private bool Peek(int offset, params LexemType[] types)
        {
            foreach (var curr in types)
            {
                var id = Math.Min(_lexemId + offset, _lexems.Length - 1);
                var lex = _lexems[id];

                if (lex.Type != curr)
                    return false;

                offset++;
            }

            return true;
        }

        /// <summary>
        /// Checks if current lexem is of any of the given types.
        /// </summary>
        private bool PeekAny(params LexemType[] types)
        {
            var id = Math.Min(_lexemId, _lexems.Length - 1);
            var lex = _lexems[id];
            return lex.Type.IsAnyOf(types);
        }

        /// <summary>
        /// Returns current lexem if it of given type, or throws an error.
        /// </summary>
        [DebuggerStepThrough]
        private Lexem Ensure(LexemType type, string msg, params object[] args)
        {
            var lex = _lexems[_lexemId];

            if (lex.Type != type)
                Error(msg, args);

            Skip();
            return lex;
        }

        /// <summary>
        /// Checks if the current lexem is of given type and advances to next one.
        /// </summary>
        [DebuggerStepThrough]
        private bool Check(LexemType lexem)
        {
            var lex = _lexems[_lexemId];

            if (lex.Type != lexem)
                return false;

            Skip();
            return true;
        }

        /// <summary>
        /// Gets the value of the current identifier and skips it.
        /// </summary>
        [DebuggerStepThrough]
        private string GetValue()
        {
            var value = _lexems[_lexemId].Value;
            Skip();
            return value;
        }

        /// <summary>
        /// Ignores N next lexems.
        /// </summary>
        [DebuggerStepThrough]
        private void Skip(int count = 1)
        {
            _lexemId = Math.Min(_lexemId + count, _lexems.Length - 1);
        }

        /// <summary>
        /// Checks if there is a newline or the block has ended.
        /// </summary>
        private bool IsStmtSeparator()
        {
            return Check(LexemType.NewLine)
                || _lexems[_lexemId - 1].Type == LexemType.Dedent;
        }

        #endregion

        #region Node handling

        /// <summary>
        /// Attempts to parse a node.
        /// If the node does not match, the parser state is silently reset to original.
        /// </summary>
        [DebuggerStepThrough]
        private T Attempt<T>(Func<T> getter)
            where T : LocationEntity
        {
            var backup = _lexemId;
            var result = Bind(getter);
            if (result == null)
                _lexemId = backup;
            return result;
        }

        /// <summary>
        /// Attempts to parse a list of values.
        /// </summary>
        [DebuggerStepThrough]
        private List<T> Attempt<T>(Func<List<T>> getter)
        {
            var backup = _lexemId;
            var result = getter();
            if (result == null || result.Count == 0)
                _lexemId = backup;
            return result;
        }

        /// <summary>
        /// Attempts to parse a node.
        /// If the node does not match, an error is thrown.
        /// </summary>
        [DebuggerStepThrough]
        private T Ensure<T>(Func<T> getter, string msg, params object[] args)
            where T : LocationEntity
        {
            var result = Bind(getter);
            if (result == null)
                Error(msg, args);

            return result;
        }

        /// <summary>
        /// Sets StartLocation and EndLocation to a node if it requires.
        /// </summary>
        [DebuggerStepThrough]
        private T Bind<T>(Func<T> getter)
            where T : LocationEntity
        {
            var startId = _lexemId;
            var start = _lexems[_lexemId];

            var result = getter();

            if (result != null)
            {
                result.StartLocation = start.StartLocation;

                var endId = _lexemId;
                if (endId > startId && endId > 0)
                    result.EndLocation = _lexems[_lexemId - 1].EndLocation;
            }

            return result;
        }

        #endregion

        #region Setters

        /// <summary>
        /// Creates a setter from a getter expression and a value to be set.
        /// </summary>
        private NodeBase MakeSetter(NodeBase getter, NodeBase expr)
        {
            if (getter is GetIdentifierNode)
            {
                var res = SetterOf(getter as GetIdentifierNode);
                res.Value = expr;
                return res;
            }

            if (getter is GetMemberNode)
            {
                var res = SetterOf(getter as GetMemberNode);
                res.Value = expr;
                return res;
            }

            if (getter is GetIndexNode)
            {
                var res = SetterOf(getter as GetIndexNode);
                res.Value = expr;
                return res;
            }

            throw new InvalidOperationException(string.Format("Node {0} is not a getter!", getter.GetType()));
        }

        /// <summary>
        /// Creates an appropriate setter for GetIdentifierNode.
        /// From: a
        /// To:   a = ...
        /// </summary>
        private SetIdentifierNode SetterOf(GetIdentifierNode node)
        {
            return new SetIdentifierNode
            {
                Identifier = node.Identifier,
                Local = node.Local
            };
        }

        /// <summary>
        /// Creates an appropriate setter for GetMemberNode.
        /// From: expr.a 
        /// To:   expr.a = ...
        /// </summary>
        private SetMemberNode SetterOf(GetMemberNode node)
        {
            return new SetMemberNode
            {
                Expression = node.Expression,
                StaticType = node.StaticType,
                MemberName = node.MemberName
            };
        }

        /// <summary>
        /// Creates an appropriate setter for GetIndexNode.
        /// From: expr[a]
        /// To:   expr[a] = ...
        /// </summary>
        private SetIndexNode SetterOf(GetIndexNode node)
        {
            return new SetIndexNode
            {
                Expression = node.Expression,
                Index = node.Index
            };
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Attaches a member of index accessor to an expression.
        /// From: x
        /// To:   x.field or x[idx]
        /// </summary>
        private static NodeBase AttachAccessor(NodeBase expr, NodeBase accessor)
        {
            if (accessor is GetMemberNode)
                (accessor as GetMemberNode).Expression = expr;
            else if (accessor is GetIndexNode)
                (accessor as GetIndexNode).Expression = expr;
            else
                throw new InvalidOperationException(string.Format("Node {0} is not an accessor!", accessor.GetType()));

            return accessor;
        }

        #endregion

        #region Operators

        /// <summary>
        /// List of binary operators and their corresponding function wrappers in precedence order.
        /// </summary>
        private static readonly List<Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>> BinaryOperatorPriorities = new List<Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>>
        {
            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.BitAnd, Expr.BitAnd},
                {LexemType.BitOr, Expr.BitOr},
                {LexemType.BitXor, Expr.BitXor},
            },

            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.And, Expr.And},
                {LexemType.Or, Expr.Or},
                {LexemType.Xor, Expr.Xor},
            },

            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.Equal, Expr.Equal},
                {LexemType.NotEqual, Expr.NotEqual},
                {LexemType.Less, Expr.Less},
                {LexemType.LessEqual, Expr.LessEqual},
                {LexemType.Greater, Expr.Greater},
                {LexemType.GreaterEqual, Expr.GreaterEqual},
            },

            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.ShiftLeft, Expr.ShiftLeft},
                {LexemType.ShiftRight, Expr.ShiftRight},
                {LexemType.DoubleQuestionMark, Expr.Coalesce }
            },

            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.Plus, Expr.Add},
                {LexemType.Minus, Expr.Sub},
            },

            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.Multiply, Expr.Mult},
                {LexemType.Divide, Expr.Div},
                {LexemType.Remainder, Expr.Mod},
            },

            new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
            {
                {LexemType.Power, Expr.Pow},
            },
        };

        /// <summary>
        /// List of unary operators and their corresponding function wrappers in precedence order.
        /// </summary>
        private static readonly Dictionary<int, Tuple<LexemType, Func<NodeBase, NodeBase>>> UnaryOperatorPriorities = new Dictionary<int, Tuple<LexemType, Func<NodeBase, NodeBase>>>
        {
            {4, new Tuple<LexemType, Func<NodeBase, NodeBase>>(LexemType.Minus, Expr.Negate)},
            {1, new Tuple<LexemType, Func<NodeBase, NodeBase>>(LexemType.Not, Expr.Not)}
        };

        /// <summary>
        /// List of binary operator lexems (for shorthand assignment checking).
        /// </summary>
        private static readonly LexemType[] BinaryOperators =
        {
            LexemType.BitAnd,
            LexemType.BitOr,
            LexemType.BitXor,
            LexemType.And,
            LexemType.Or,
            LexemType.Xor,
            LexemType.ShiftLeft,
            LexemType.ShiftRight,
            LexemType.Plus,
            LexemType.Minus,
            LexemType.Multiply,
            LexemType.Divide,
            LexemType.Remainder,
            LexemType.Power
        };

        /// <summary>
        /// Recursively creates a tree of binary expressions according to operator precedence.
        /// </summary>
        /// <param name="getter">Function that returns the expression.</param>
        /// <param name="priority">Current priority.</param>
        private NodeBase ProcessOperator(Func<NodeBase> getter, int priority = 0)
        {
            if (priority == BinaryOperatorPriorities.Count)
                return Bind(getter);

            var unaryCvt = UnaryOperatorPriorities.ContainsKey(priority) && Check(UnaryOperatorPriorities[priority].Item1)
                ? UnaryOperatorPriorities[priority].Item2
                : null;

            var node = Bind(() => ProcessOperator(getter, priority + 1));
            if (node == null)
                return null;

            if (unaryCvt != null)
                node = unaryCvt(node);

            var ops = BinaryOperatorPriorities[priority];
            while (PeekAny(ops.Keys.ToArray()))
            {
                foreach (var curr in ops)
                    if (Check(curr.Key))
                        node = curr.Value(node, Ensure(() => ProcessOperator(getter, priority + 1), ParserMessages.ExpressionExpected));
            }

            return node;
        }

        #endregion
    }
}