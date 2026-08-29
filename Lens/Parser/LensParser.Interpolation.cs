using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Literals;
using Lens.Translations;

namespace Lens.Parser
{
    internal partial class LensParser
    {
        #region Interpolated strings

        /// <summary>
        /// interpolated_string                         = "$" [ "@" ] '"' { text | "{" line_expr [ ":" format ] "}" } '"'
        /// </summary>
        private NodeBase ParseInterpolatedString()
        {
            if (!Peek(LexemType.InterpolatedString))
                return null;

            var lexem = _lexems[_lexemId];
            Skip();

            var parts = lexem.InterpolationParts;

            // no holes at all: an ordinary string literal
            if (parts.All(p => !p.IsHole))
                return new StringNode(string.Concat(parts.Select(p => p.Literal)));

            var format = new StringBuilder();
            var args = new List<NodeBase>();

            foreach (var curr in parts)
            {
                if (!curr.IsHole)
                {
                    // braces of a literal chunk must stay escaped in the format string
                    format.Append(curr.Literal.Replace("{", "{{").Replace("}", "}}"));
                    continue;
                }

                format.Append('{');
                format.Append(args.Count);

                if (!string.IsNullOrEmpty(curr.Format))
                {
                    format.Append(':');
                    format.Append(curr.Format);
                }

                format.Append('}');

                args.Add(ParseInterpolationHole(curr));
            }

            var invocationArgs = new List<NodeBase> {Expr.Str(format.ToString())};
            invocationArgs.AddRange(args);

            return Expr.Invoke(
                Expr.GetMember(typeof(string).FullName, nameof(string.Format)),
                invocationArgs.ToArray()
            );
        }

        /// <summary>
        /// Lexes and parses the contents of a single hole as a standalone expression.
        /// All locations are shifted so that they point at the hole's position in the outer source.
        /// </summary>
        private NodeBase ParseInterpolationHole(InterpolatedStringPart part)
        {
            List<Lexem> lexems;
            try
            {
                lexems = new LensLexer(part.Expression).Lexems;
            }
            catch (LensCompilerException ex)
            {
                throw Shift(ex, part.StartLocation);
            }

            foreach (var curr in lexems)
            {
                curr.StartLocation = part.Shift(curr.StartLocation);
                curr.EndLocation = part.Shift(curr.EndLocation);
            }

            var nodes = new LensParser(lexems).Nodes;
            if (nodes.Count != 1)
                Error(ParserMessages.InterpolationHoleExpressionExpected);

            return nodes[0];
        }

        /// <summary>
        /// Rebinds an exception thrown by a nested parse to its real position in the outer source.
        /// </summary>
        private static LensCompilerException Shift(LensCompilerException ex, LexemLocation origin)
        {
            return ex.BindToLocation(
                InterpolatedStringPart.Shift(ex.StartLocation ?? default(LexemLocation), origin),
                InterpolatedStringPart.Shift(ex.EndLocation ?? default(LexemLocation), origin)
            );
        }

        #endregion
    }
}
