using System;
using Lens.Stdlib;

namespace Lens.Compiler
{
	internal partial class Context
	{
		private void initStdlib()
		{
			importFunction("times", typeof(Utilities).GetMethod("TimesIndex"));
			importFunction("times", typeof(Utilities).GetMethod("Times"));
			importFunction("fail", typeof(Utilities).GetMethod("FailWith"));

			importOverloads(typeof(string), "Format", "fmt");
			importOverloads(typeof(Console), "Write", "print");
			importOverloads(typeof(Console), "WriteLine", "println");

			importFunction("rand", typeof(Randomizer).GetMethod("Random"));
			importFunction("rand", typeof(Randomizer).GetMethod("RandomMax"));
			importFunction("rand", typeof(Randomizer).GetMethod("RandomMinMax"));
			importFunction("rand", typeof(Randomizer).GetMethod("RandomOf"));
			importFunction("rand", typeof(Randomizer).GetMethod("RandomOfWeight"));

			importFunction("read", typeof(Console).GetMethod("Read"));
			importFunction("readln", typeof(Console).GetMethod("ReadLine"));
			importFunction("readkey", typeof(ConsoleWrapper).GetMethod("ReadKey"));
			importFunction("waitkey", typeof(ConsoleWrapper).GetMethod("WaitKey"));

			importFunction("clamp", typeof(Utilities).GetMethod("ClampInt"));
			importFunction("clamp", typeof(Utilities).GetMethod("ClampFloat"));
			importFunction("clamp", typeof(Utilities).GetMethod("ClampDouble"));
			importFunction("clamp", typeof(Utilities).GetMethod("ClampLong"));

			importFunction("to", typeof(Utilities).GetMethod("RangeInt"));
			importFunction("to", typeof(Utilities).GetMethod("RangeIntStep"));
			importFunction("to", typeof(Utilities).GetMethod("RangeString"));
			importFunction("to", typeof(Utilities).GetMethod("RangeStringStep"));

			importFunction("odd", typeof(Utilities).GetMethod("OddInt"));
			importFunction("odd", typeof(Utilities).GetMethod("OddLong"));
			importFunction("even", typeof(Utilities).GetMethod("EvenInt"));
			importFunction("even", typeof(Utilities).GetMethod("EvenLong"));
		}
	}
}
