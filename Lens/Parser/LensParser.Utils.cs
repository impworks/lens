using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Expressions;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Parser
{
	internal partial class LensParser
	{
		#region Error reporting

		[DebuggerStepThrough]
		private void error(string msg, params object[] args)
		{
			throw new LensCompilerException(
				string.Format(msg, args),
				_Lexems[_LexemId]
			);
		}

		#endregion

		#region Lexem handling

		/// <summary>
		/// Checks if the pattern at current location matches given one.
		/// </summary>
		[DebuggerStepThrough]
		private bool peek(params LexemType[] types)
		{
			return peek(0, types);
		}

		/// <summary>
		/// Checks if the pattern at offset matches given one.
		/// </summary>
		[DebuggerStepThrough]
		private bool peek(int offset, params LexemType[] types)
		{
			foreach (var curr in types)
			{
				var id = Math.Min(_LexemId + offset, _Lexems.Length - 1);
				var lex = _Lexems[id];

				if (lex.Type != curr)
					return false;

				offset++;
			}

			return true;
		}

		/// <summary>
		/// Checks if current lexem is of any of the given types.
		/// </summary>
		private bool peekAny(params LexemType[] types)
		{
			var id = Math.Min(_LexemId, _Lexems.Length - 1);
			var lex = _Lexems[id];
			return lex.Type.IsAnyOf(types);
		}

		/// <summary>
		/// Returns current lexem if it of given type, or throws an error.
		/// </summary>
		[DebuggerStepThrough]
		private Lexem ensure(LexemType type, string msg, params object[] args)
		{
			var lex = _Lexems[_LexemId];

			if(lex.Type != type)
				error(msg, args);

			skip();
			return lex;
		}

		/// <summary>
		/// Checks if the current lexem is of given type and advances to next one.
		/// </summary>
		[DebuggerStepThrough]
		private bool check(LexemType lexem)
		{
			var lex = _Lexems[_LexemId];

			if (lex.Type != lexem)
				return false;

			skip();
			return true;
		}

		/// <summary>
		/// Gets the value of the current identifier and skips it.
		/// </summary>
		[DebuggerStepThrough]
		private string getValue()
		{
			var value = _Lexems[_LexemId].Value;
			skip();
			return value;
		}

		/// <summary>
		/// Ignores N next lexems.
		/// </summary>
		[DebuggerStepThrough]
		private void skip(int count = 1)
		{
			_LexemId = Math.Min(_LexemId + count, _Lexems.Length - 1);
		}

		/// <summary>
		/// Checks if there is a newline or the block has ended.
		/// </summary>
		private bool isStmtSeparator()
		{
			return check(LexemType.NewLine) || _Lexems[_LexemId - 1].Type == LexemType.Dedent;
		}

		#endregion

		#region Node handling

		/// <summary>
		/// Attempts to parse a node.
		/// If the node does not match, the parser state is silently reset to original.
		/// </summary>
		[DebuggerStepThrough]
		private T attempt<T>(Func<T> getter)
			where T : LocationEntity
		{
			var backup = _LexemId;
			var result = bind(getter);
			if (result == null)
				_LexemId = backup;
			return result;
		}

		/// <summary>
		/// Attempts to parse a list of values.
		/// </summary>
		[DebuggerStepThrough]
		private List<T> attempt<T>(Func<List<T>> getter)
		{
			var backup = _LexemId;
			var result = getter();
			if (result == null || result.Count == 0)
				_LexemId = backup;
			return result;
		}

			/// <summary>
		/// Attempts to parse a node.
		/// If the node does not match, an error is thrown.
		/// </summary>
		[DebuggerStepThrough]
		private T ensure<T>(Func<T> getter, string msg)
			where T : LocationEntity
		{
			var result = bind(getter);
			if(result == null)
				error(msg);

			return result;
		}

		/// <summary>
		/// Sets StartLocation and EndLocation to a node if it requires.
		/// </summary>
		[DebuggerStepThrough]
		private T bind<T>(Func<T> getter)
			where T : LocationEntity
		{
			var startId = _LexemId;
			var start = _Lexems[_LexemId];

			var result = getter();

			if (result != null)
			{
				result.StartLocation = start.StartLocation;

				var endId = _LexemId;
				if (endId > startId && endId > 0)
					result.EndLocation = _Lexems[_LexemId - 1].EndLocation;
			}

			return result;
		}

		#endregion

		#region Setters

		/// <summary>
		/// Creates a setter from a getter expression and a value to be set.
		/// </summary>
		private NodeBase makeSetter(NodeBase getter, NodeBase expr)
		{
			if (getter is GetIdentifierNode)
			{
				var res = setterOf(getter as GetIdentifierNode);
				res.Value = expr;
				return res;
			}

			if (getter is GetMemberNode)
			{
				var res = setterOf(getter as GetMemberNode);
				res.Value = expr;
				return res;
			}

			if (getter is GetIndexNode)
			{
				var res = setterOf(getter as GetIndexNode);
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
		private SetIdentifierNode setterOf(GetIdentifierNode node)
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
		private SetMemberNode setterOf(GetMemberNode node)
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
		private SetIndexNode setterOf(GetIndexNode node)
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
		private static NodeBase attachAccessor(NodeBase expr, NodeBase accessor)
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
		private static readonly List<Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>> _BinaryOperatorPriorities = new List<Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>>
		{
			new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
			{
				{ LexemType.And, Expr.And },
				{ LexemType.Or, Expr.Or },
				{ LexemType.Xor, Expr.Xor },
			},

			new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
			{
				{ LexemType.Equal, Expr.Equal },
				{ LexemType.NotEqual, Expr.NotEqual },
				{ LexemType.Less, Expr.Less },
				{ LexemType.LessEqual, Expr.LessEqual },
				{ LexemType.Greater, Expr.Greater },
				{ LexemType.GreaterEqual, Expr.GreaterEqual },
			},

			new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
			{
				{ LexemType.ShiftLeft, Expr.ShiftLeft },
				{ LexemType.ShiftRight, Expr.ShiftRight },
			},

			new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
			{
				{ LexemType.Plus, Expr.Add },
				{ LexemType.Minus, Expr.Sub },
			},

			new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
			{
				{ LexemType.Multiply, Expr.Mult },
				{ LexemType.Divide, Expr.Div },
				{ LexemType.Remainder, Expr.Mod },
			},

			new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
			{
				{ LexemType.Power, Expr.Pow },
			},
		};

		/// <summary>
		/// List of unary operators and their corresponding function wrappers in precedence order.
		/// </summary>
		private static readonly Dictionary<int, Tuple<LexemType, Func<NodeBase, NodeBase>>> _UnaryOperatorPriorities = new Dictionary<int, Tuple<LexemType, Func<NodeBase, NodeBase>>>
		{
			{ 3, new Tuple<LexemType, Func<NodeBase, NodeBase>>(LexemType.Minus, Expr.Negate) },
			{ 0, new Tuple<LexemType, Func<NodeBase, NodeBase>>(LexemType.Not, Expr.Not) }
		};

		/// <summary>
		/// List of binary operator lexems (for shorthand assignment checking).
		/// </summary>
		private static readonly LexemType[] _BinaryOperators =
		{
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
		private NodeBase processOperator(Func<NodeBase> getter, int priority = 0)
		{
			if (priority == _BinaryOperatorPriorities.Count)
				return bind(getter);

			var unaryCvt = _UnaryOperatorPriorities.ContainsKey(priority) && check(_UnaryOperatorPriorities[priority].Item1)
				? _UnaryOperatorPriorities[priority].Item2
				: null;
			
			var node = bind(() => processOperator(getter, priority + 1));
			if (unaryCvt != null)
				node = unaryCvt(node);

			var ops = _BinaryOperatorPriorities[priority];
			while (peekAny(ops.Keys.ToArray()))
			{
				foreach (var curr in ops)
					if (check(curr.Key))
						node = curr.Value(node, ensure(() => processOperator(getter, priority + 1), ParserMessages.ExpressionExpected));
			}

			return node;
		}

		#endregion
	}
}
