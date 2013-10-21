using System.Linq;
using Lens.Lexer;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	class LexerTest
	{
		[Test]
		public void StartNewlines()
		{
			var str = @"


let a = 1";

			Test(str,
				LexemType.Let,
				LexemType.Identifier,
				LexemType.Assign,
				LexemType.Int,
				LexemType.EOF
			);
		}

		[Test]
		public void EndNewlines()
		{
			var str = @"let a = 1



";

			Test(str,
				LexemType.Let,
				LexemType.Identifier,
				LexemType.Assign,
				LexemType.Int,
				LexemType.EOF
			);
		}


		[Test]
		public void BetweenNewlines()
		{
			var str = @"a = 1


b = 2";

			Test(str,
				LexemType.Identifier,
				LexemType.Assign,
				LexemType.Int,
				LexemType.NewLine,
				LexemType.Identifier,
				LexemType.Assign,
				LexemType.Int,
				LexemType.EOF
			);
		}

		private void Test(string str, params LexemType[] types)
		{
			var lexer = new LensLexer(str);
			Assert.AreEqual(lexer.Lexems.Select(l => l.Type).ToArray(), types);
		}
	}
}
