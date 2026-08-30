using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lens.SyntaxTree;

namespace Lens.Utils
{
    /// <summary>
    /// A named group of a regex literal, and where it is written.
    ///
    /// The name of a group is a variable declaration - '#(?&lt;num:int&gt;[0-9]+)#' introduces 'num'
    /// exactly as 'case num:int' would - so an editor has the same questions about it as about any
    /// other name, and needs the same answer: where in the file it is written.
    /// </summary>
    internal class RegexGroup
    {
        #region Constructor

        public RegexGroup(string name, string typeName, LocationEntity nameLocation)
        {
            Name = name;
            TypeName = typeName;
            NameLocation = nameLocation;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name the group binds.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The name of the type the captured text is converted to, or null when it stays a string.
        /// </summary>
        public readonly string TypeName;

        /// <summary>
        /// Where the name itself is written, delimiters and type label excluded.
        /// </summary>
        public readonly LocationEntity NameLocation;

        #endregion
    }

    /// <summary>
    /// Reading a regex literal - '#pattern#modifiers' - the way both the compiler and the editor
    /// need it read.
    ///
    /// The compiler wants the pattern that .NET's Regex accepts and the names it binds; the editor
    /// wants those names' positions in the file, because the lexer hands the whole literal over as
    /// a single lexem and a caret on a group name has to find something to answer with. Both come
    /// out of the same scan, so it lives here rather than in either of them.
    /// </summary>
    internal static class RegexLiteral
    {
        #region Static constants

        /// <summary>
        /// A named group's header, with the type label LENS adds to it.
        /// </summary>
        private static readonly Regex NamedGroupPattern = new Regex(
            @"\(\?<(?<name>[a-z0-9_]+)(?::(?<type>[a-z\0-9_]+))?>",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase
        );

        #endregion

        #region Methods

        /// <summary>
        /// Splits a regex lexem - '#pattern#modifiers' - into its two halves.
        /// </summary>
        public static void Split(string literal, out string pattern, out string modifiers)
        {
            var trailing = literal.LastIndexOf('#');

            pattern = literal.Substring(1, trailing - 1);
            modifiers = literal.Substring(trailing + 1);
        }

        /// <summary>
        /// The named groups a pattern declares, each with the position of its name in the source.
        /// </summary>
        /// <param name="pattern">The pattern, as it is written between the delimiters.</param>
        /// <param name="start">Where the literal's opening delimiter sits in the source.</param>
        public static IEnumerable<RegexGroup> NamedGroups(string pattern, LexemLocation start)
        {
            if (string.IsNullOrEmpty(pattern))
                yield break;

            foreach (Match group in NamedGroupPattern.Matches(pattern))
            {
                if (!group.Success)
                    continue;

                var name = group.Groups["name"];
                var type = group.Groups["type"];

                yield return new RegexGroup(
                    name.Value,
                    type.Success && type.Value.Length > 0 ? type.Value : null,
                    new LocationEntity
                    {
                        StartLocation = Shift(start, pattern, name.Index),
                        EndLocation = Shift(start, pattern, name.Index + name.Length)
                    }
                );
            }
        }

        /// <summary>
        /// The pattern as .NET's Regex accepts it: the ':type' label is an addition of ours and
        /// means nothing to it.
        /// </summary>
        public static string StripTypes(string pattern)
        {
            return NamedGroupPattern.Replace(pattern, "(?<$1>");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The source position of a character of the pattern.
        ///
        /// The pattern has already been unescaped by the time it gets here, so a '#' in it stands
        /// for the two characters the source spells it with and every column after it is shifted
        /// by one. Lines are not counted, because the lexer does not count them inside a lexem
        /// either - a literal that spans lines is mislocated identically by both.
        /// </summary>
        private static LexemLocation Shift(LexemLocation start, string pattern, int index)
        {
            // the opening delimiter is not part of the pattern
            var offset = start.Offset + 1 + index;

            for (var idx = 0; idx < index; idx++)
            {
                if (pattern[idx] == '#')
                    offset++;
            }

            return new LexemLocation {Line = start.Line, Offset = offset};
        }

        #endregion
    }
}
