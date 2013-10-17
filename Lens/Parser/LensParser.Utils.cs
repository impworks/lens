using System;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.Utils;

namespace Lens.Parser
{
	internal partial class LensParser
	{
		private void Error(string msg)
		{
			throw new LensCompilerException(msg, Lexems[LexemId]);
		}

		#region Lexem handling

		/// <summary>
		/// Checks if current lexem is of any of the given types.
		/// </summary>
		private bool peekLexem(params LexemType[] types)
		{
			return peekLexem(0, types);
		}

		/// <summary>
		/// Checks if lexem at offset is of any of the given types.
		/// </summary>
		private bool peekLexem(int offset, params LexemType[] types)
		{
			var id = Math.Min(LexemId + offset, Lexems.Length - 1);
			var lex = Lexems[id];
			return lex.Type.IsAnyOf(types);
		}

		/// <summary>
		/// Returns current lexem and advances to next one.
		/// </summary>
		private Lexem getLexem()
		{
			var lex = Lexems[LexemId];
			skipLexem();
			return lex;
		}

		private void skipLexem(int count = 1)
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
			where T : NodeBase
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
		private T ensure<T>(Func<T> getter, string error)
			where T : NodeBase
		{
			var result = bind(getter);
			if(result == null)
				Error(error);

			return result;
		}

		/// <summary>
		/// Sets StartLocation and EndLocation to a node if it requires.
		/// </summary>
		private T bind<T>(Func<T> getter)
			where T : NodeBase
		{
			var start = Lexems[LexemId];
			var result = getter();
			var end = Lexems[LexemId-1];

			if (result is IStartLocationTrackingEntity)
				result.StartLocation = start.StartLocation;

			if (result is IEndLocationTrackingEntity)
				result.EndLocation = end.EndLocation;

			return result;
		}

		#endregion
	}
}
