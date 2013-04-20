using System;
using Lens.SyntaxTree.Stdlib;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		public void InitStdlib()
		{
			ImportFunctionUnchecked("times", typeof(Utilities).GetMethod("TimesIndex"));
			ImportFunctionUnchecked("times", typeof(Utilities).GetMethod("Times"));
			ImportFunctionUnchecked("fail", typeof(Utilities).GetMethod("FailWith"));

			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format1"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format2"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format3"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format4"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format5"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format6"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format7"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format8"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format9"));
			ImportFunctionUnchecked("fmt", typeof(Utilities).GetMethod("Format10"));

			ImportFunctionUnchecked("rand", typeof(Randomizer).GetMethod("Random"));
			ImportFunctionUnchecked("rand", typeof(Randomizer).GetMethod("RandomMax"));
			ImportFunctionUnchecked("rand", typeof(Randomizer).GetMethod("RandomMinMax"));
			ImportFunctionUnchecked("rand", typeof(Randomizer).GetMethod("RandomOf"));
			ImportFunctionUnchecked("rand", typeof(Randomizer).GetMethod("RandomOfWeight"));
			
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print1"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print2"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print3"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print4"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print5"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print6"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print7"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print8"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print9"));
			ImportFunctionUnchecked("print", typeof(ConsoleWrapper).GetMethod("Print10"));

			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine1"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine2"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine3"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine4"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine5"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine6"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine7"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine8"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine9"));
			ImportFunctionUnchecked("println", typeof(ConsoleWrapper).GetMethod("PrintLine10"));

			ImportFunctionUnchecked("read", typeof(Console).GetMethod("Read"));
			ImportFunctionUnchecked("readln", typeof(Console).GetMethod("ReadLine"));
			ImportFunctionUnchecked("readkey", typeof(ConsoleWrapper).GetMethod("ReadKey"));
			ImportFunctionUnchecked("waitkey", typeof(ConsoleWrapper).GetMethod("WaitKey"));
		}
	}
}
