using System;
using Lens.Stdlib;

namespace Lens.Compiler
{
	internal partial class Context
	{
		/// <summary>
		/// Initializes the standard library.
		/// </summary>
		private void initStdlib()
		{
			importOverloads(typeof(Utilities), "Times", "times");
			importFunction("fail", typeof(Utilities).GetMethod("FailWith"));

			importOverloads(typeof(string), "Format", "fmt");
			importOverloads(typeof(Console), "Write", "print");
			importOverloads(typeof(Console), "WriteLine", "println");

			importOverloads(typeof(Randomizer), "Random", "rand");
			importOverloads(typeof(Utilities), "Range", "to");
			importOverloads(typeof(Utilities), "Clamp", "clamp");
			importOverloads(typeof(Utilities), "Odd", "odd");
			importOverloads(typeof(Utilities), "Even", "even");

			importFunction("read", typeof(Console).GetMethod("Read"));
			importFunction("readln", typeof(Console).GetMethod("ReadLine"));
			importFunction("readkey", typeof(ConsoleWrapper).GetMethod("ReadKey"));
			importFunction("waitkey", typeof(ConsoleWrapper).GetMethod("WaitKey"));
		}
	}
}
