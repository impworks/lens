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
            ImportFunction("fail", typeof(Utilities).GetMethod("FailWith"), false);

            ImportOverloads(typeof(string), "Format", "fmt");
            ImportOverloads(typeof(Console), "Write", "print");
            ImportOverloads(typeof(Console), "WriteLine", "println");

            ImportOverloads(typeof(Randomizer), "Random", "rand");
            ImportOverloads(typeof(Utilities), "Range", "to");
            ImportOverloads(typeof(Utilities), "Clamp", "clamp");
            ImportOverloads(typeof(Utilities), "Odd", "odd");
            ImportOverloads(typeof(Utilities), "Even", "even");

            ImportFunction("read", typeof(Console).GetMethod("Read"), false);
            ImportFunction("readln", typeof(Console).GetMethod("ReadLine"), false);
            ImportFunction("readkey", typeof(ConsoleWrapper).GetMethod("ReadKey"), false);
            ImportFunction("waitkey", typeof(ConsoleWrapper).GetMethod("WaitKey"), false);
        }
    }
}