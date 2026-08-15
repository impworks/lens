using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Types;
using Lens.SyntaxTree.Expressions.GetSet;

namespace Lens.Analysis
{
    /// <summary>
    /// Where a name comes from and everywhere else it is written.
    ///
    /// Two mechanisms, because two kinds of name work differently. A local has a symbol object with
    /// a declaration and a reference list, built during binding, so its references are exact. A
    /// global - a function, a record, an algebraic type - has no such object, but it also has no
    /// scope: LENS puts every one of them in a single namespace and gives user types no methods, so
    /// an identifier spelling that name and not reached through a '.' is that name. Locals are
    /// subtracted first, so a local shadowing a function is never mistaken for it.
    /// </summary>
    public sealed partial class ScriptAnalysis
    {
        #region Symbol lookup

        /// <summary>
        /// The name the caret is on, or null if it is not on one.
        /// </summary>
        public ScriptSymbol FindSymbol(LexemLocation position)
        {
            var lexem = LexemAt(position);
            if (lexem == null || lexem.Type != LexemType.Identifier)
                return null;

            return FindLocalSymbol(position)
                   ?? FindGlobalSymbol(lexem)
                   ?? FindMemberSymbol(lexem);
        }

        /// <summary>
        /// A variable, an argument or a pattern binding: the names binding gave a symbol to.
        /// </summary>
        private ScriptSymbol FindLocalSymbol(LexemLocation position)
        {
            foreach (var local in _context.LocalSymbols)
            {
                var spans = LocalSpans(local);
                if (!spans.Any(x => Contains(x, position)))
                    continue;

                var isArgument = local.ArgumentId != null;

                return new ScriptSymbol(
                    local.Name,
                    isArgument ? SymbolKind.Parameter : SymbolKind.Local,
                    $"{(local.IsImmutable ? "let" : "var")} {local.Name} : {TypeName(local.Type)}",
                    spans.Count > 0 ? spans[0] : (TextSpan?) null,
                    spans,
                    !HasSyntaxErrors,
                    HasSyntaxErrors ? RenameRefusedForSyntax : null
                );
            }

            return null;
        }

        /// <summary>
        /// A function, a record or an algebraic type - whether the script declares it or the host
        /// provides it.
        /// </summary>
        private ScriptSymbol FindGlobalSymbol(Lexem lexem)
        {
            var name = lexem.Value;

            // a field is a global name at its declaration and a member everywhere else, so both
            // paths lead to the same symbol
            var owner = FindRecordDeclaring(lexem);
            if (owner != null)
                return BuildRecordFieldSymbol(owner, name);

            var declaration = FindDeclaration(name);

            if (declaration == null)
            {
                // not declared here: it may still be something the environment offers, which is
                // worth describing on hover even though it cannot be renamed
                var external = DescribeExternal(name);
                if (external == null)
                    return null;

                return new ScriptSymbol(name, external.Item1, external.Item2, null, new[] {SpanOf(lexem)}, false, RenameRefusedForExternal);
            }

            var spans = GlobalSpans(name);

            return new ScriptSymbol(
                name,
                declaration.Item1,
                declaration.Item2,
                declaration.Item3,
                spans,
                !HasSyntaxErrors,
                HasSyntaxErrors ? RenameRefusedForSyntax : null
            );
        }

        /// <summary>
        /// Something reached through a '.' or a '::'.
        ///
        /// A field of a record the script declares is the one case where this is the script's own
        /// name, and it gets a real reference list: the receiver's bound type says which record a
        /// mention belongs to, so two records with a field of the same name never get confused.
        /// Everything else is a .NET member and is hover only.
        /// </summary>
        private ScriptSymbol FindMemberSymbol(Lexem lexem)
        {
            var index = IndexOfLexem(lexem);
            if (index <= 0)
                return null;

            var previous = _lexer.Lexems[index - 1];
            if (previous.Type != LexemType.Dot && previous.Type != LexemType.NullSafeDot && previous.Type != LexemType.DoubleСolon)
                return null;

            if (previous.Type != LexemType.DoubleСolon)
            {
                var field = FindRecordFieldSymbol(previous, lexem.Value);
                if (field != null)
                    return field;
            }

            return new ScriptSymbol(lexem.Value, SymbolKind.Member, lexem.Value, null, new[] {SpanOf(lexem)}, false, RenameRefusedForExternal);
        }

        /// <summary>
        /// A field of a script-declared record, named through a receiver whose type is known.
        /// </summary>
        private ScriptSymbol FindRecordFieldSymbol(Lexem dot, string name)
        {
            // the receiver ends exactly where the dot begins
            var receiver = TypeEndingAt(dot.StartLocation);

            return ReferenceEquals(receiver, null)
                ? null
                : BuildRecordFieldSymbol(receiver.Name, name);
        }

        /// <summary>
        /// The record whose declaration the caret is inside, when the caret is on one of its field
        /// names.
        /// </summary>
        private string FindRecordDeclaring(Lexem lexem)
        {
            foreach (var curr in _parser.Nodes.OfType<RecordDefinitionNode>())
            {
                foreach (var field in curr.Entries)
                {
                    if (field.Name == lexem.Value && Contains(SpanOf(field), lexem.StartLocation))
                        return curr.Name;
                }
            }

            return null;
        }

        /// <summary>
        /// A record field, with every mention of it that binding could account for.
        /// </summary>
        private ScriptSymbol BuildRecordFieldSymbol(string recordName, string name)
        {
            var entity = _context.FindType(recordName);
            if (entity == null || entity.IsImported || !entity.HasField(name))
                return null;

            var record = _parser.Nodes.OfType<RecordDefinitionNode>().FirstOrDefault(x => x.Name == recordName);
            var declared = record?.Entries.FirstOrDefault(x => x.Name == name);

            var spans = new List<TextSpan>();

            if (declared != null)
                spans.Add(NarrowToName(SpanOf(declared), name));

            foreach (var curr in MemberSpans(entity.TypeInfo, name))
            {
                if (!spans.Any(x => SameSpan(x, curr)))
                    spans.Add(curr);
            }

            return new ScriptSymbol(
                name,
                SymbolKind.RecordField,
                $"{recordName}.{name} : {TypeName(entity.ResolveField(name)?.Type)}",
                declared != null ? spans[0] : (TextSpan?) null,
                spans,
                declared != null && !HasSyntaxErrors,
                declared == null
                    ? RenameRefusedForExternal
                    : HasSyntaxErrors ? RenameRefusedForSyntax : null
            );
        }

        /// <summary>
        /// Every place a field of a given type is named through a receiver.
        /// </summary>
        private IEnumerable<TextSpan> MemberSpans(Resolver.TypeEntry owner, string name)
        {
            foreach (var curr in AllNodes())
            {
                if (!(curr is MemberNodeBase member) || member.MemberName != name)
                    continue;

                var expression = (curr as GetMemberNode)?.Expression ?? (curr as SetMemberNode)?.Expression;
                if (expression == null)
                    continue;

                var type = _context.FindExpressionType(expression);
                if (ReferenceEquals(type, null) || type != owner)
                    continue;

                var span = NarrowToName(SpanOf(curr), name);
                if (!span.IsEmpty)
                    yield return span;
            }
        }

        /// <summary>
        /// The position of a lexem in the stream, by identity.
        ///
        /// List.IndexOf would use Lexem's structural equality and answer with the first lexem that
        /// merely looks the same, which for a name that appears twice is the wrong one.
        /// </summary>
        private int IndexOfLexem(Lexem lexem)
        {
            for (var i = 0; i < _lexer.Lexems.Count; i++)
            {
                if (ReferenceEquals(_lexer.Lexems[i], lexem))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// What to show when the caret rests on something: what the name is, or failing that, the
        /// type of the expression it belongs to.
        /// </summary>
        public string DescribeAt(LexemLocation position)
        {
            var symbol = FindSymbol(position);
            if (symbol != null)
                return symbol.Detail;

            var lexem = LexemAt(position);
            if (lexem == null)
                return null;

            var type = TypeEndingAt(lexem.EndLocation);
            return ReferenceEquals(type, null) ? null : TypeName(type);
        }

        #endregion

        #region Reference sets

        /// <summary>
        /// Every place a local is written, its declaration first.
        /// </summary>
        private IReadOnlyList<TextSpan> LocalSpans(Local local)
        {
            var result = new List<TextSpan>();

            if (local.Declaration != null)
            {
                // the declaration entity is the whole statement - 'let x = 1' - and what is being
                // renamed is the name inside it
                var narrowed = NarrowToName(SpanOf(local.Declaration), local.Name);
                if (!narrowed.IsEmpty)
                    result.Add(narrowed);
            }

            foreach (var curr in local.References)
            {
                var span = SpanOf(curr);
                if (!span.IsEmpty && !result.Any(x => SameSpan(x, span)))
                    result.Add(span);
            }

            return result;
        }

        /// <summary>
        /// Every identifier that names a global, which is every identifier spelling it that is
        /// neither reached through a '.' nor claimed by a local.
        /// </summary>
        private IReadOnlyList<TextSpan> GlobalSpans(string name)
        {
            var taken = new HashSet<string>(
                _context.LocalSymbols.SelectMany(LocalSpans).Select(Key)
            );

            var result = new List<TextSpan>();

            for (var i = 0; i < _lexer.Lexems.Count; i++)
            {
                var curr = _lexer.Lexems[i];

                if (curr.Type != LexemType.Identifier || curr.Value != name)
                    continue;

                if (i > 0)
                {
                    var previous = _lexer.Lexems[i - 1].Type;
                    if (previous == LexemType.Dot || previous == LexemType.NullSafeDot || previous == LexemType.DoubleСolon)
                        continue;
                }

                var span = SpanOf(curr);
                if (!taken.Contains(Key(span)))
                    result.Add(span);
            }

            return result;
        }

        #endregion

        #region Declarations

        /// <summary>
        /// What the script itself declares under a name: its kind, how to describe it, and where
        /// the name is written.
        /// </summary>
        private System.Tuple<SymbolKind, string, TextSpan?> FindDeclaration(string name)
        {
            foreach (var curr in _parser.Nodes)
            {
                if (curr is FunctionNode fun && fun.Name == name)
                    return System.Tuple.Create(SymbolKind.Function, Describe(fun), (TextSpan?) SpanOf(fun.NameLocation));

                // record fields are not looked up here: they are members, and which record a
                // mention belongs to is a question about the receiver's type rather than the name
                if (curr is RecordDefinitionNode record && record.Name == name)
                    return System.Tuple.Create(SymbolKind.Record, "record " + name, (TextSpan?) SpanOf(record.NameLocation));

                if (curr is TypeDefinitionNode type)
                {
                    if (type.Name == name)
                        return System.Tuple.Create(SymbolKind.AlgebraicType, "type " + name, (TextSpan?) SpanOf(type.NameLocation));

                    var label = type.Entries.FirstOrDefault(x => x.Name == name);
                    if (label != null)
                        return System.Tuple.Create(SymbolKind.TypeLabel, $"{type.Name}.{name}", (TextSpan?) NarrowToName(SpanOf(label), name));
                }

                if (curr is DeclarationBlockNode block)
                {
                    var entry = FindDeclaredEntry(block, name);
                    if (entry != null)
                        return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// What a 'declare' block says about a name.
        /// </summary>
        private static System.Tuple<SymbolKind, string, TextSpan?> FindDeclaredEntry(DeclarationBlockNode block, string name)
        {
            foreach (var curr in block.Entries)
            {
                if (curr is DeclaredProperty property && property.Name == name)
                    return System.Tuple.Create(SymbolKind.GlobalVariable, $"{(property.IsMutable ? "var" : "let")} {name} : {property.Type}", (TextSpan?) SpanOf(curr));

                if (curr is DeclaredFunction function && function.Name == name)
                    return System.Tuple.Create(SymbolKind.Function, Describe(function), (TextSpan?) SpanOf(curr));

                if (curr is DeclaredTypeAlias alias && alias.Alias == name)
                    return System.Tuple.Create(SymbolKind.HostType, $"type {name} = {alias.Type}", (TextSpan?) SpanOf(curr));
            }

            return null;
        }

        /// <summary>
        /// What the environment offers under a name that the script does not declare: a host type,
        /// a standard library function, a registered global.
        /// </summary>
        private System.Tuple<SymbolKind, string> DescribeExternal(string name)
        {
            var type = _context.DefinedTypes.FirstOrDefault(x => x.Key == name);
            if (type.Value != null)
                return System.Tuple.Create(SymbolKind.HostType, type.Value.TypeInfo?.FullName ?? name);

            var property = _context.DefinedProperties.FirstOrDefault(x => x.Key == name);
            if (property.Value != null)
                return System.Tuple.Create(SymbolKind.GlobalVariable, $"{name} : {property.Value.PropertyType.Name}");

            if (_context.MainType.HasMethodGroup(name))
            {
                var overloads = _context.MainType.ResolveMethodGroup(name);
                return System.Tuple.Create(SymbolKind.Function, string.Join("\n", overloads.Select(x => Describe(x))));
            }

            return null;
        }

        #endregion

        #region Descriptions

        private static string Describe(FunctionNode node)
        {
            var args = string.Join(" ", node.Arguments.Select(x => $"{x.Name}:{x.TypeSignature}"));
            return $"fun {node.Name}{ReturnOf(node.ReturnTypeSignature?.FullSignature)}{(args.Length > 0 ? " (" + args + ")" : "")}";
        }

        private static string Describe(DeclaredFunction node)
        {
            var args = string.Join(" ", node.Arguments.Select(x => $"{x.Name}:{x.TypeSignature}"));
            return $"fun {node.Name}{ReturnOf(node.ReturnTypeSignature?.FullSignature)}{(args.Length > 0 ? " (" + args + ")" : "")}";
        }

        /// <summary>
        /// Describes a method of a declared type.
        /// </summary>
        /// <param name="method">The method as the declaration writes it.</param>
        /// <param name="reference">
        /// The type the method is reached through, when that is an instantiation of the declaration:
        /// the signature is then written in the terms of the use site rather than of the parameters
        /// the declaration named.
        /// </param>
        private string Describe(MethodEntity method, Resolver.TypeEntry reference = null)
        {
            Resolver.TypeEntry substitute(Resolver.TypeEntry type) =>
                reference == null ? type : _context.MemberTypeOf(reference, type);

            var args = string.Join(" ", method.Arguments.Values.Select(x => $"{x.Name}:{TypeName(substitute(x.GetArgumentType(_context)))}"));
            return $"fun {method.Name}:{TypeName(substitute(method.ReturnType))}{(args.Length > 0 ? " (" + args + ")" : "")}";
        }

        private static string ReturnOf(string signature)
        {
            return string.IsNullOrEmpty(signature) ? "" : ":" + signature;
        }

        private static string TypeName(Resolver.TypeEntry type)
        {
            return TypeNames.Of(type);
        }

        #endregion

        #region Helpers

        private const string RenameRefusedForSyntax = "The file has to parse before anything in it can be renamed.";
        private const string RenameRefusedForExternal = "This name belongs to the environment, not to the script.";

        /// <summary>
        /// Shrinks a span that covers a whole declaration down to the name inside it.
        /// </summary>
        private TextSpan NarrowToName(TextSpan span, string name)
        {
            foreach (var curr in _lexer.Lexems)
            {
                if (curr.Type != LexemType.Identifier || curr.Value != name)
                    continue;

                if (Contains(span, curr.StartLocation))
                    return SpanOf(curr);
            }

            return span;
        }

        private static bool SameSpan(TextSpan left, TextSpan right)
        {
            return left.Start.Line == right.Start.Line
                   && left.Start.Offset == right.Start.Offset
                   && left.End.Line == right.End.Line
                   && left.End.Offset == right.End.Offset;
        }

        private static string Key(TextSpan span)
        {
            return $"{span.Start.Line}:{span.Start.Offset}";
        }

        #endregion
    }
}
