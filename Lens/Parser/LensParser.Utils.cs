using System;
using System.IO;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Expressions;

namespace Lens.Parser
{
	internal partial class LensParser
	{
		private void error(string msg, params object[] args)
		{
			throw new LensCompilerException(
				string.Format(msg, args),
				Lexems[LexemId]
			);
		}

		#region Lexem handling

		/// <summary>
		/// Checks if current lexem is of any of the given types.
		/// </summary>
		private bool peek(params LexemType[] types)
		{
			return peek(0, types);
		}

		/// <summary>
		/// Checks if lexem at offset is of any of the given types.
		/// </summary>
		private bool peek(int offset, params LexemType[] types)
		{
			foreach (var curr in types)
			{
				var id = Math.Min(LexemId + offset, Lexems.Length - 1);
				var lex = Lexems[id];

				if (lex.Type != curr)
					return false;

				offset++;
			}

			return true;
		}

		/// <summary>
		/// Returns current lexem if it of given type, or throws an error.
		/// </summary>
		private Lexem ensure(LexemType type, string msg, params object[] args)
		{
			var lex = Lexems[LexemId];

			if(lex.Type != type)
				error(msg, args);

			skip();
			return lex;
		}

		/// <summary>
		/// Checks if the current lexem is of given type and advances to next one.
		/// </summary>
		private bool check(LexemType lexem)
		{
			var lex = Lexems[LexemId];

			if (lex.Type != lexem)
				return false;

			skip();
			return true;
		}

		/// <summary>
		/// Gets the value of the current identifier and skips it.
		/// </summary>
		private string getValue()
		{
			var value = Lexems[LexemId].Value;
			skip();
			return value;
		}

		/// <summary>
		/// Ignores N next lexems.
		/// </summary>
		private void skip(int count = 1)
		{
			LexemId = Math.Min(LexemId + count, Lexems.Length - 1);
		}

		#endregion

		#region Node handling

		/// <summary>
		/// Attempts to parse a node.
		/// If the node does not match, the parser state is silently reset to original.
		/// </summary>
		private T attempt<T>(Func<T> getter)
			where T : LocationEntity
		{
			var backup = LexemId;
			var result = bind(getter);
			if (result == null)
				LexemId = backup;
			return result;
		}

		/// <summary>
		/// Attempts to parse a node.
		/// If the node does not match, an error is thrown.
		/// </summary>
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
		private T bind<T>(Func<T> getter)
			where T : LocationEntity
		{
			var startId = LexemId;
			var start = Lexems[LexemId];

			var result = getter();

			if (result is IStartLocationTrackingEntity)
				result.StartLocation = start.StartLocation;

			var endId = LexemId;
			if (endId > startId && endId > 0 && result is IEndLocationTrackingEntity)
				result.EndLocation = Lexems[LexemId - 1].EndLocation;

			return result;
		}

		#endregion

		#region Setters

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

		private SetIdentifierNode setterOf(GetIdentifierNode node)
		{
			return new SetIdentifierNode
			{
				Identifier = node.Identifier,
				LocalName = node.LocalName
			};
		}

		private SetMemberNode setterOf(GetMemberNode node)
		{
			return new SetMemberNode
			{
				Expression = node.Expression,
				StaticType = node.StaticType,
				MemberName = node.MemberName
			};
		}

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

		private NodeBase attachAccessor(NodeBase node, NodeBase accessor)
		{
			if (accessor is GetMemberNode)
				(accessor as GetMemberNode).Expression = node;
			else if (accessor is GetIndexNode)
				(accessor as GetIndexNode).Expression = node;
			else
				throw new InvalidOperationException(string.Format("Node {0} is not an accessor!", accessor.GetType()));

			return accessor;
		}

		#endregion
	}
}
