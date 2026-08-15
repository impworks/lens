using System;
using Lens.Analysis;

namespace Lens.LanguageServer.Core
{
    /// <summary>
    /// The names an editor knows colours by, and which of them each kind of LENS token maps to.
    ///
    /// The list is the standard one from the language server protocol. Editors that use different
    /// names still recognise these, and the ones that do not fall back to their own grammar.
    /// </summary>
    public static class SemanticTokenLegend
    {
        /// <summary>
        /// The token types, in the order their indices refer to.
        /// </summary>
        public static readonly string[] TokenTypes =
        {
            "keyword",
            "variable",
            "type",
            "function",
            "parameter",
            "property",
            "number",
            "string",
            "regexp",
            "operator"
        };

        /// <summary>
        /// No modifiers are produced, but a legend has to declare the list it will not use.
        /// </summary>
        public static readonly string[] TokenModifiers = new string[0];

        /// <summary>
        /// The index a LENS token kind is coloured under.
        /// </summary>
        public static int IndexOf(TokenKind kind)
        {
            switch (kind)
            {
                case TokenKind.Keyword: return 0;
                case TokenKind.Identifier: return 1;
                case TokenKind.Variable: return 1;
                case TokenKind.Type: return 2;
                case TokenKind.Function: return 3;
                case TokenKind.Parameter: return 4;
                case TokenKind.Field: return 5;
                case TokenKind.Number: return 6;
                case TokenKind.String: return 7;
                case TokenKind.Regex: return 8;
                case TokenKind.Operator: return 9;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
