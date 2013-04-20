using System;
using Lens.SyntaxTree.Stdlib;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		public void InitStdlib()
		{
			ImportFunction("times", typeof(Utilities).GetMethod("TimesIndex"));
			ImportFunction("times", typeof(Utilities).GetMethod("Times"));
			ImportFunction("fail", typeof(Utilities).GetMethod("FailWith"));

			ImportFunction("fmt", typeof(Utilities).GetMethod("Format1"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format2"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format3"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format4"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format5"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format6"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format7"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format8"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format9"));
			ImportFunction("fmt", typeof(Utilities).GetMethod("Format10"));

			ImportFunction("rand", typeof(Randomizer).GetMethod("Random"));
			ImportFunction("rand", typeof(Randomizer).GetMethod("RandomMax"));
			ImportFunction("rand", typeof(Randomizer).GetMethod("RandomMinMax"));
			ImportFunction("rand", typeof(Randomizer).GetMethod("RandomOf"));
			ImportFunction("rand", typeof(Randomizer).GetMethod("RandomOfWeight"));
			
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print1"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print2"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print3"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print4"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print5"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print6"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print7"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print8"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print9"));
			ImportFunction("print", typeof(ConsoleWrapper).GetMethod("Print10"));

			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine1"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine2"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine3"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine4"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine5"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine6"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine7"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine8"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine9"));
			ImportFunction("println", typeof(ConsoleWrapper).GetMethod("PrintLine10"));

			ImportFunction("read", typeof(Console).GetMethod("Read"));
			ImportFunction("readln", typeof(Console).GetMethod("ReadLine"));
			ImportFunction("readkey", typeof(ConsoleWrapper).GetMethod("ReadKey"));
			ImportFunction("waitkey", typeof(ConsoleWrapper).GetMethod("WaitKey"));
		}
	}
}
