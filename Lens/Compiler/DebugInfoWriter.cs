using System;
using System.Reflection.Emit;
using Lens.SyntaxTree;

#if NET_SYMBOLS
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
#endif

namespace Lens.Compiler
{
    /// <summary>
    /// Records where in the script each piece of emitted IL came from, so that a debugger attached
    /// to the host can show the script rather than the instructions it turned into.
    ///
    /// The two supported platforms disagree about which symbol format is written and about who
    /// writes it, but they agree on the interface used to describe a position - so everything the
    /// compiler says about its own output is said here once, and the difference is confined to how
    /// the emit target was created and what happens to it afterwards.
    /// </summary>
    internal class DebugInfoWriter
    {
        #region Constants

        /// <summary>
        /// The identifier LENS claims for itself in a symbol file.
        ///
        /// Stable from the first release, because a symbol file outlives the compiler that wrote it -
        /// but see <see cref="LensDebugSettings.ReportAsCSharp"/> for why it is not what is written
        /// by default.
        /// </summary>
        public static readonly Guid LanguageGuid = new Guid("da68a4e9-47b7-49b6-a85f-e53a56ff1f8a");

        /// <summary>
        /// The identifier C# uses.
        ///
        /// A debugger picks the code it evaluates watches and hover tooltips with by the language a
        /// symbol file names, and no debugger has an evaluator for LENS. Claiming to be C# is what
        /// gets one - and it is largely true of the expressions a debugger is asked to evaluate,
        /// which are names of variables and fields, spelled the same in both languages.
        /// </summary>
        public static readonly Guid CSharpLanguageGuid = new Guid("3f5162f8-07c6-11d3-9053-00c04fa302a1");

        /// <summary>
        /// The line number that marks a stretch of IL as belonging to no line of the script.
        ///
        /// This is not a convention of this compiler: every symbol format and every .NET debugger
        /// spells a hidden sequence point this way.
        /// </summary>
        private const int HiddenLine = 0xFEEFEE;

        #endregion

        #region Constructor

#if NET_SYMBOLS
        public DebugInfoWriter(ISymbolDocumentWriter document, string source)
        {
            Document = document;
            _marked = new Dictionary<ILGenerator, int>();
            _lines = source?.Split('\n');
        }
#endif

        #endregion

        #region Fields

#if NET_SYMBOLS
        /// <summary>
        /// The source file every sequence point refers to.
        /// </summary>
        public readonly ISymbolDocumentWriter Document;

        /// <summary>
        /// The offset at which each method last had a sequence point placed.
        ///
        /// Two sequence points at one offset are not a description of anything: the second silently
        /// replaces the first in some readers and corrupts the sequence in others. A statement that
        /// emits nothing - one the binder folded away, or a declaration whose value was inlined -
        /// leaves the offset where the previous statement left it, so this is not a rare case.
        /// </summary>
        private readonly Dictionary<ILGenerator, int> _marked;

        /// <summary>
        /// The lines of the script, used to work out where one of them ends.
        /// Null when the script was handed to the compiler as a tree and no source exists.
        /// </summary>
        private readonly string[] _lines;
#endif

        #endregion

        #region Methods

        /// <summary>
        /// Marks the IL that follows as the code of one statement.
        ///
        /// A node the compiler synthesized has no position of its own, and is marked hidden instead:
        /// stepping through a script must not stop inside machinery its author never wrote.
        /// </summary>
        public void MarkStatement(ILGenerator gen, LocationEntity node)
        {
            if (gen == null)
                return;

            var start = node?.StartLocation ?? default(LexemLocation);
            if (start.Line <= 0)
            {
                MarkHidden(gen);
                return;
            }

            var end = node.EndLocation;

            // A statement is highlighted on the line it starts on and no further. The end recorded
            // on a node is not the end of the statement: it is wherever the parser had got to, which
            // for anything with a body is past the whole body, and even for a plain assignment is
            // past the blank lines after it. Left alone, a debugger stopped on the first line of a
            // loop highlights the loop entire, and stepping round it looks like standing still.
            //
            // Ending on the starting line is not a compromise here. LENS separates statements by
            // newlines, so a statement is a line - which is exactly the unit a debugger stops on.
            if (end.Line != start.Line || end.Offset <= start.Offset)
                end = new LexemLocation {Line = start.Line, Offset = EndOfLine(start)};

            Mark(gen, start.Line, Math.Max(start.Offset, 1), end.Line, Math.Max(end.Offset, 1));
        }

        /// <summary>
        /// Marks the IL that follows as belonging to no line of the script.
        ///
        /// Nothing is marked at the very start of a method: a point placed there could not later be
        /// replaced by the first statement's, and every debugger already treats the instructions
        /// before the first sequence point as the prologue and steps over them.
        /// </summary>
        public void MarkHidden(ILGenerator gen)
        {
            if (gen == null || gen.ILOffset == 0)
                return;

            Mark(gen, HiddenLine, 0, HiddenLine, 0);
        }

        /// <summary>
        /// Records the name a local variable has in the script, so that a debugger can show it
        /// under that name rather than as a numbered slot.
        ///
        /// A slot the compiler invented for itself is left nameless: it holds an intermediate value
        /// that corresponds to nothing the author of the script can reason about.
        /// </summary>
        public void NameLocal(LocalBuilder local, string name)
        {
            if (local == null || string.IsNullOrEmpty(name) || IsSynthetic(name))
                return;

#if NET_SYMBOLS
            local.SetLocalSymInfo(name);
#endif
        }

        /// <summary>
        /// Checks whether a name was invented by the compiler rather than written in the script.
        ///
        /// Every such name is spelled with angle brackets, which the LENS grammar has no way of
        /// producing - the same trick, and for the same reason, that C# uses.
        /// </summary>
        public static bool IsSynthetic(string name)
        {
            return string.IsNullOrEmpty(name) || name[0] == '<';
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The column just past the last character of the line a statement starts on, so that the
        /// highlight covers the statement and stops there.
        ///
        /// Falls back to a single character where the source is not at hand - a script compiled
        /// from a tree the host built itself has no text to measure.
        /// </summary>
        private int EndOfLine(LexemLocation start)
        {
            var fallback = start.Offset + 1;

#if NET_SYMBOLS
            if (_lines == null || start.Line > _lines.Length)
                return fallback;

            var length = _lines[start.Line - 1].TrimEnd('\r', '\n', ' ', '\t').Length;
            return Math.Max(length + 1, fallback);
#else
            return fallback;
#endif
        }

        /// <summary>
        /// Places one sequence point, unless the previous one is still standing at this offset.
        /// </summary>
        private void Mark(ILGenerator gen, int startLine, int startOffset, int endLine, int endOffset)
        {
#if NET_SYMBOLS
            if (_marked.TryGetValue(gen, out var previous) && previous == gen.ILOffset)
                return;

            gen.MarkSequencePoint(Document, startLine, startOffset, endLine, endOffset);
            _marked[gen] = gen.ILOffset;
#endif
        }

        #endregion
    }
}
