using System;
using Lens.Stdlib;

namespace Lens.Compiler
{
	internal partial class Context
	{
		/// <summary>
		/// Initializes the standard library.
		/// </summary>
		private void InitStdlib()
		{
			ImportOverloads(typeof(Utilities), "Times", "times");
			importFunction("fail", typeof(Utilities).GetMethod("FailWith"));

			ImportOverloads(typeof(string), "Format", "fmt");
			ImportOverloads(typeof(Console), "Write", "print");
			ImportOverloads(typeof(Console), "WriteLine", "println");

			ImportOverloads(typeof(Randomizer), "Random", "rand");
			ImportOverloads(typeof(Utilities), "Range", "to");
			ImportOverloads(typeof(Utilities), "Clamp", "clamp");
			ImportOverloads(typeof(Utilities), "Odd", "odd");
			ImportOverloads(typeof(Utilities), "Even", "even");

			importFunction("read", typeof(Console).GetMethod("Read"));
			importFunction("readln", typeof(Console).GetMethod("ReadLine"));
			importFunction("readkey", typeof(ConsoleWrapper).GetMethod("ReadKey"));
			importFunction("waitkey", typeof(ConsoleWrapper).GetMethod("WaitKey"));
		}
	}
}
