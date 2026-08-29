using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Locals;
using Lens.SyntaxTree.Declarations.Types;

namespace Lens.Analysis
{
    /// <summary>
    /// The shape of the file: what it declares, in the order it declares it.
    ///
    /// Built from the parse tree alone, so it still works on a file that does not bind - which is
    /// most of the time in an editor, and exactly when an outline is worth having.
    /// </summary>
    public sealed partial class ScriptAnalysis
    {
        #region Outline

        /// <summary>
        /// Every declaration in the file, nested where the language nests them.
        /// </summary>
        private IReadOnlyList<OutlineItem> BuildOutline()
        {
            var result = new List<OutlineItem>();

            foreach (var curr in _parser.Nodes)
            {
                var item = OutlineOf(curr);
                if (item != null)
                    result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// The outline entry a top-level node produces, if it produces one.
        /// </summary>
        private OutlineItem OutlineOf(SyntaxTree.NodeBase node)
        {
            if (node is FunctionNode fun)
            {
                return new OutlineItem(fun.Name, SymbolKind.Function, Describe(fun), SpanOf(fun), SpanOf(fun.NameLocation), NoChildren);
            }

            if (node is RecordDefinitionNode record)
            {
                var fields = record.Entries
                                   .Select(x => new OutlineItem(x.Name, SymbolKind.RecordField, x.Type?.FullSignature, SpanOf(x), NarrowToName(SpanOf(x), x.Name), NoChildren))
                                   .ToArray();

                return new OutlineItem(record.Name, SymbolKind.Record, "record", SpanOf(record), SpanOf(record.NameLocation), fields);
            }

            if (node is TypeDefinitionNode type)
            {
                var labels = type.Entries
                                 .Select(x => new OutlineItem(x.Name, SymbolKind.TypeLabel, x.TagType?.FullSignature, SpanOf(x), NarrowToName(SpanOf(x), x.Name), NoChildren))
                                 .ToArray();

                return new OutlineItem(type.Name, SymbolKind.AlgebraicType, "type", SpanOf(type), SpanOf(type.NameLocation), labels);
            }

            if (node is DeclarationBlockNode block)
            {
                var entries = block.Entries.Select(OutlineOfEntry).Where(x => x != null).ToArray();
                return new OutlineItem("declare", SymbolKind.Keyword, "environment", SpanOf(block), SpanOf(block), entries);
            }

            if (node is NameDeclarationNodeBase name)
            {
                var kind = name.IsImmutable ? "let" : "var";
                return new OutlineItem(name.Name, SymbolKind.Local, kind, SpanOf(name), NarrowToName(SpanOf(name), name.Name), NoChildren);
            }

            return null;
        }

        /// <summary>
        /// The outline entry for one line of a 'declare' block.
        /// </summary>
        private OutlineItem OutlineOfEntry(DeclarationEntryBase entry)
        {
            if (entry is DeclaredProperty property)
                return new OutlineItem(property.Name, SymbolKind.GlobalVariable, property.Type?.FullSignature, SpanOf(entry), NarrowToName(SpanOf(entry), property.Name), NoChildren);

            if (entry is DeclaredFunction function)
                return new OutlineItem(function.Name, SymbolKind.Function, Describe(function), SpanOf(entry), NarrowToName(SpanOf(entry), function.Name), NoChildren);

            if (entry is DeclaredTypeAlias alias)
                return new OutlineItem(alias.Alias, SymbolKind.HostType, alias.Type?.FullSignature, SpanOf(entry), NarrowToName(SpanOf(entry), alias.Alias), NoChildren);

            if (entry is DeclaredReference reference)
            {
                // an assembly path is a string literal and a string literal may be empty, but an
                // outline entry has to be called something
                var path = string.IsNullOrWhiteSpace(reference.Path) ? "reference" : reference.Path;
                return new OutlineItem(path, SymbolKind.Keyword, "reference", SpanOf(entry), SpanOf(entry), NoChildren);
            }

            return null;
        }

        private static readonly OutlineItem[] NoChildren = new OutlineItem[0];

        #endregion

        #region References

        /// <summary>
        /// The assemblies the file asks for, for tooling to resolve and complain about.
        /// </summary>
        public IReadOnlyList<ReferencedAssembly> References =>
            _references ?? (_references = _parser.Nodes
                                                 .OfType<DeclarationBlockNode>()
                                                 .SelectMany(x => x.Entries)
                                                 .OfType<DeclaredReference>()
                                                 .Select(x => new ReferencedAssembly(x.Path, SpanOf(x)))
                                                 .ToArray());

        private IReadOnlyList<ReferencedAssembly> _references;

        #endregion
    }
}
