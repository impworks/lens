namespace Lens
{
    /// <summary>
    /// The settings that control what debug information a compilation produces.
    ///
    /// Debug information is what lets a host attach a debugger - Visual Studio, Rider - to itself
    /// and step through the LENS script it runs, rather than only through the code that calls it.
    /// It is off by default: producing it costs a slower compilation, a larger assembly and an
    /// unoptimized script, none of which a host running a script in production wants to pay for.
    /// </summary>
    public class LensDebugSettings
    {
        /// <summary>
        /// Whether debug information is generated at all.
        ///
        /// Supported on net47 and on net10.0 or later, and not on netstandard2.0 - the API that
        /// writes symbols is missing from the netstandard surface, so a compilation that asks for
        /// them there is refused rather than silently producing none.
        ///
        /// Default = false.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// The path recorded in the symbols as the script's source file.
        ///
        /// A host that read the script from disk should point this at the file it read, so that the
        /// debugger opens the very file its author edits. A host that built the script in memory can
        /// leave it unset and rely on the source being embedded instead.
        ///
        /// Default = none.
        /// </summary>
        public string SourceFile;

        /// <summary>
        /// Whether the script's own text is stored inside the symbols.
        ///
        /// This is what makes a script that lives in memory debuggable at all: the debugger has no
        /// file to open, so it reads the source out of the symbols. Where a source file does exist,
        /// embedding it as well costs a little space and removes any chance of the debugger finding
        /// a file that has been edited since the script was compiled.
        ///
        /// Only the portable symbol format can carry the source, so this has no effect on net47.
        ///
        /// Default = true.
        /// </summary>
        public bool EmbedSource = true;

        /// <summary>
        /// Whether the symbols name C# as the language the script is written in.
        ///
        /// A debugger chooses the evaluator behind its watch window and its hover tooltips by the
        /// language a symbol file names, and none of them has an evaluator for LENS. Naming C#
        /// borrows its evaluator, which is what makes hovering over a variable show its value. The
        /// borrowing holds for what a debugger is actually asked to evaluate - the name of a
        /// variable, a field, an element - because LENS spells those as C# does.
        ///
        /// Turn it off to have the symbols say LENS, which is honest and costs the tooltips.
        ///
        /// Default = true.
        /// </summary>
        public bool ReportAsCSharp = true;
    }
}
