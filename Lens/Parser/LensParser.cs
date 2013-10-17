using System.Collections.Generic;
using System.Linq;
using Lens.Lexer;
using Lens.SyntaxTree;

namespace Lens.Parser
{
	internal partial class LensParser
	{
		public List<NodeBase> Nodes { get; private set; }

		private Lexem[] Lexems;
		private int LexemId;

		public LensParser(IEnumerable<Lexem> lexems)
		{
			Lexems = lexems.ToArray();

			Nodes = new List<NodeBase>();

			// todo : parse
		}
	}
}
