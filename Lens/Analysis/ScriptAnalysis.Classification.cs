using System.Collections.Generic;
using System.Linq;
using Lens.Lexer;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Types;

namespace Lens.Analysis
{
    /// <summary>
    /// Turning identifiers into the thing they actually name.
    ///
    /// This is the half of colouring an editor cannot do for itself. A regular expression can tell
    /// a keyword from a number; only the compiler can tell that 'Point' is a record, that 'radius'
    /// is an argument and that 'clamp' is a function the host registered.
    /// </summary>
    public sealed partial class ScriptAnalysis
    {
        #region Classification

        /// <summary>
        /// Works out what each identifier in the file names.
        /// </summary>
        private SemanticClassification BuildSemanticClassification()
        {
            var result = new SemanticClassification();

            ClassifyLocals(result);
            ClassifyDeclarations(result);
            ClassifyTypeReferences(result);
            ClassifyRemainingNames(result);

            return result;
        }

        /// <summary>
        /// Every place the source wrote a type signature.
        ///
        /// A signature is not an identifier - 'System.Collections.Generic.List&lt;Point&gt;' is one
        /// signature and seven names - so every name inside its span is coloured as part of it.
        /// </summary>
        private void ClassifyTypeReferences(SemanticClassification target)
        {
            foreach (var curr in _context.TypeReferences)
            {
                var span = SpanOf(curr.Key);
                if (span.IsEmpty)
                    continue;

                foreach (var lexem in _lexer.Lexems)
                {
                    if (lexem.Type != LexemType.Identifier)
                        continue;

                    if (Contains(span, lexem.StartLocation, false))
                        target.Add(SpanOf(lexem), TokenKind.Type);
                }
            }
        }

        /// <summary>
        /// Variables, arguments and pattern bindings, from the symbols binding produced.
        /// </summary>
        private void ClassifyLocals(SemanticClassification target)
        {
            foreach (var curr in _context.LocalSymbols)
            {
                var kind = curr.ArgumentId != null ? TokenKind.Parameter : TokenKind.Variable;

                foreach (var span in LocalSpans(curr))
                    target.Add(span, kind);
            }
        }

        /// <summary>
        /// The names the script declares, which are known from the parse tree alone and so survive
        /// a file that does not bind.
        /// </summary>
        private void ClassifyDeclarations(SemanticClassification target)
        {
            foreach (var curr in _parser.Nodes)
            {
                if (curr is FunctionNode fun)
                    target.Add(SpanOf(fun.NameLocation), TokenKind.Function);

                if (curr is RecordDefinitionNode record)
                {
                    target.Add(SpanOf(record.NameLocation), TokenKind.Type);

                    foreach (var field in record.Entries)
                        target.Add(NarrowToName(SpanOf(field), field.Name), TokenKind.Field);
                }

                if (curr is TypeDefinitionNode type)
                {
                    target.Add(SpanOf(type.NameLocation), TokenKind.Type);

                    foreach (var label in type.Entries)
                        target.Add(NarrowToName(SpanOf(label), label.Name), TokenKind.Type);
                }

                if (curr is DeclarationBlockNode block)
                    ClassifyDeclaredEntries(target, block);
            }
        }

        /// <summary>
        /// The environment a 'declare' block describes.
        /// </summary>
        private void ClassifyDeclaredEntries(SemanticClassification target, DeclarationBlockNode block)
        {
            foreach (var curr in block.Entries)
            {
                if (curr is DeclaredProperty property)
                    target.Add(NarrowToName(SpanOf(curr), property.Name), TokenKind.Variable);

                else if (curr is DeclaredFunction function)
                    target.Add(NarrowToName(SpanOf(curr), function.Name), TokenKind.Function);

                else if (curr is DeclaredTypeAlias alias)
                    target.Add(NarrowToName(SpanOf(curr), alias.Alias), TokenKind.Type);

                // 'reference' is a contextual keyword: the lexer hands it over as an identifier
                // like any other, and only the entry it opens says that it is not one
                else if (curr is DeclaredReference)
                    target.Add(NarrowToName(SpanOf(curr), "reference"), TokenKind.Keyword);
            }
        }

        /// <summary>
        /// Everything else the environment offers: host types, standard library functions and
        /// registered globals, wherever the script names one.
        /// </summary>
        private void ClassifyRemainingNames(SemanticClassification target)
        {
            for (var i = 0; i < _lexer.Lexems.Count; i++)
            {
                var curr = _lexer.Lexems[i];

                if (curr.Type != LexemType.Identifier)
                    continue;

                var span = SpanOf(curr);
                if (target.Classify(span) != null)
                    continue;

                // a name after a '.' is a member of whatever came before it, and belongs to that
                // type rather than to any of the script's own namespaces
                if (i > 0)
                {
                    var previous = _lexer.Lexems[i - 1].Type;
                    if (previous == LexemType.Dot || previous == LexemType.NullSafeDot)
                        continue;
                }

                var kind = ClassifyEnvironmentName(curr.Value);
                if (kind != null)
                    target.Add(span, kind.Value);
            }
        }

        /// <summary>
        /// What the environment calls a name, if it knows it at all.
        /// </summary>
        private TokenKind? ClassifyEnvironmentName(string name)
        {
            if (_context.DefinedTypes.Any(x => x.Key == name))
                return TokenKind.Type;

            if (_context.MainType.HasMethodGroup(name))
                return TokenKind.Function;

            if (_context.DefinedProperties.Any(x => x.Key == name))
                return TokenKind.Variable;

            return null;
        }

        #endregion

        #region Classification table

        /// <summary>
        /// What each identifier in the file was found to name, keyed by where it starts.
        /// </summary>
        private sealed class SemanticClassification
        {
            private readonly Dictionary<string, TokenKind> _kinds = new Dictionary<string, TokenKind>();

            /// <summary>
            /// Records what a name is. The first answer wins: locals and declarations are looked
            /// up before the broader sources, and are the more specific of the two.
            /// </summary>
            public void Add(TextSpan span, TokenKind kind)
            {
                if (span.IsEmpty || _kinds.ContainsKey(Key(span)))
                    return;

                _kinds[Key(span)] = kind;
            }

            public TokenKind? Classify(TextSpan span)
            {
                return _kinds.TryGetValue(Key(span), out var kind) ? kind : (TokenKind?) null;
            }
        }

        #endregion
    }
}
