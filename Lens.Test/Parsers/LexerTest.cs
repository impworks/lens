using System.Linq;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Parsers
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

		[Test]
		public void StringErrorLocation()
		{
			try
			{
				var src = @"let x = ""hello";
				new LensLexer(src);
			}
			catch (LensCompilerException ex)
			{
				Assert.AreEqual(LexerMessages.UnclosedString, ex.Message);
				Assert.AreEqual(new LexemLocation {Line = 1, Offset = 9}, ex.StartLocation);
				Assert.AreEqual(new LexemLocation { Line = 1, Offset = 15 }, ex.EndLocation);
			}
			catch
			{
				Assert.Fail("Incorrect exception type!");
			}
		}

		private void Test(string str, params LexemType[] types)
		{
			var lexer = new LensLexer(str);
			Assert.AreEqual(types, lexer.Lexems.Select(l => l.Type).ToArray());
		}
	}
}
