using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Types;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.Expressions.Instantiation;

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

            var symbol = FindLocalSymbol(position) ?? FindGlobalSymbol(lexem);

            // a type written after a 'new' is being called, not merely named, and which of its
            // constructors will be reached is the question the arguments about to be typed answer
            var constructors = FindConstructors(lexem);

            // a segment of a dotted type name is not a member access, however much it looks like
            // one: the 'StringBuilder' of 'new System.Text.StringBuilder' is part of the name
            if (constructors == null)
                return symbol ?? FindMemberSymbol(lexem);

            return symbol == null
                ? new ScriptSymbol(lexem.Value, SymbolKind.HostType, constructors, null, new[] {SpanOf(lexem)}, false, RenameRefusedForExternal)
                : symbol.WithDetail(symbol.Detail + "\n" + constructors);
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

            var previous = Lexems[index - 1];
            if (previous.Type != LexemType.Dot && previous.Type != LexemType.NullSafeDot && previous.Type != LexemType.DoubleСolon)
                return null;

            var isStatic = previous.Type == LexemType.DoubleСolon;

            if (!isStatic)
            {
                var field = FindRecordFieldSymbol(previous, lexem.Value);
                if (field != null)
                    return field;
            }

            // the name on its own says nothing the line under the pointer does not already say:
            // what a reader wants of 'Substring' is what it takes and what it gives back, and with
            // several overloads under the one name, which of them they are about
            var detail = DescribeMember(ReceiverOf(previous, isStatic), isStatic, lexem.Value);

            return new ScriptSymbol(lexem.Value, SymbolKind.Member, detail ?? lexem.Value, null, new[] {SpanOf(lexem)}, false, RenameRefusedForExternal);
        }

        /// <summary>
        /// The type a member is being read from: the type named to the left of a '::', or the type
        /// of the expression ending at a '.'.
        /// </summary>
        private Resolver.TypeEntry ReceiverOf(Lexem accessor, bool isStatic)
        {
            return isStatic
                ? FindStaticReceiver(IndexOf(accessor.StartLocation))?.Type
                : TypeEndingAt(accessor.StartLocation);
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
        /// The constructors of the type named at a position, when the position is inside the type
        /// of a 'new'. Null everywhere else: nothing is being constructed there.
        /// </summary>
        private string FindConstructors(Lexem lexem)
        {
            return DescribeConstructors(ConstructedType(lexem));
        }

        /// <summary>
        /// The type a 'new' at a position builds.
        ///
        /// The bound tree answers first, because it is the only thing that knows what the generic
        /// arguments came out as. It answers for very little of the time an editor spends, though,
        /// and none of the time that matters: 'new string' is halfway through being typed and does
        /// not parse, and 'new string 'x'' parses but matches no constructor and so binds to
        /// nothing - and those are exactly the moments somebody wants to be told what the
        /// constructors are. The text answers when the tree cannot; the type is written in both.
        /// </summary>
        private Resolver.TypeEntry ConstructedType(Lexem lexem)
        {
            foreach (var curr in AllNodes())
            {
                if (!(curr is NewObjectNode created) || created.TypeSignature == null)
                    continue;

                if (!Contains(SpanOf(created.TypeSignature), lexem.StartLocation) || !NamesTheType(created.TypeSignature, lexem.StartLocation))
                    continue;

                return created.Type ?? _context.FindExpressionType(curr) ?? ResolvedType(created.TypeSignature);
            }

            return ResolvedType(SignatureAfterNew(lexem));
        }

        /// <summary>
        /// The type signature the caret is inside of, when it is written directly after a 'new'.
        ///
        /// Read off the lexem stream rather than off the tree, because there may be no tree: a name
        /// is dotted, and it carries generic arguments that are names in their own right, so the
        /// run of lexems between the 'new' and the arguments of the call is the whole of it.
        /// </summary>
        private TypeSignature SignatureAfterNew(Lexem lexem)
        {
            var index = IndexOfLexem(lexem);
            if (index <= 0)
                return null;

            var start = index;
            while (start > 0 && IsSignatureLexem(Lexems[start - 1].Type))
                start--;

            if (start == 0 || Lexems[start - 1].Type != LexemType.New)
                return null;

            // the 'int' of 'new List<int>' is a question about int, not about the list
            for (var idx = start; idx < index; idx++)
            {
                if (Lexems[idx].Type == LexemType.Less)
                    return null;
            }

            var end = index;
            while (end + 1 < Lexems.Count && IsSignatureLexem(Lexems[end + 1].Type))
                end++;

            var from = IndexOf(Lexems[start].StartLocation);
            var to = IndexOf(Lexems[end].EndLocation);

            if (from < 0 || to <= from)
                return null;

            try
            {
                return TypeSignature.Parse(Source.Substring(from, to - from));
            }
            catch (Exception)
            {
                // half of a generic argument list is not a signature yet
                return null;
            }
        }

        /// <summary>
        /// Whether a lexem can be part of a type name. A '.' separates the segments of one, and the
        /// angle brackets and commas belong to its generic arguments.
        /// </summary>
        private static bool IsSignatureLexem(LexemType type)
        {
            switch (type)
            {
                case LexemType.Identifier:
                case LexemType.Dot:
                case LexemType.Less:
                case LexemType.Greater:
                case LexemType.Comma:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// What a signature names, or null when it names nothing this script can see.
        /// </summary>
        private Resolver.TypeEntry ResolvedType(TypeSignature signature)
        {
            if (signature == null)
                return null;

            var direct = TryResolve(signature);
            if (!ReferenceEquals(direct, null))
                return direct;

            // 'new List' is halfway to 'new List<int>' and names nothing on its own, but the
            // definition it is halfway to still says what the constructors take. Answering in the
            // terms of the declaration beats answering nothing while somebody is still typing.
            if (signature.Arguments != null || signature.Name == null)
                return null;

            for (var arity = 1; arity <= MaxInferredArity; arity++)
            {
                var candidate = TryResolve(new TypeSignature(signature.Name + "`" + arity));
                if (!ReferenceEquals(candidate, null))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// The one attempt: a signature either names a type or throws.
        /// </summary>
        private Resolver.TypeEntry TryResolve(TypeSignature signature)
        {
            try
            {
                return _context.ResolveType(signature);
            }
            catch (Exception)
            {
                // not a type, or not one this script can see
                return null;
            }
        }

        /// <summary>
        /// How many generic parameters a name with none written is guessed at. Past a handful the
        /// guess is worse than silence - and nothing anybody constructs by hand has that many.
        /// </summary>
        private const int MaxInferredArity = 4;

        /// <summary>
        /// Whether a position falls on the name of a type signature rather than inside one of its
        /// generic arguments: the 'int' of 'new List&lt;int&gt;' is a question about int.
        /// </summary>
        private static bool NamesTheType(TypeSignature signature, LexemLocation position)
        {
            if (signature.Arguments == null)
                return true;

            foreach (var curr in signature.Arguments)
            {
                if (Contains(SpanOf(curr), position))
                    return false;
            }

            return true;
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
            for (var i = 0; i < Lexems.Count; i++)
            {
                if (ReferenceEquals(Lexems[i], lexem))
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

            for (var i = 0; i < Lexems.Count; i++)
            {
                var curr = Lexems[i];

                if (curr.Type != LexemType.Identifier || curr.Value != name)
                    continue;

                if (i > 0)
                {
                    var previous = Lexems[i - 1].Type;
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
            // functions first, and all of them at once: overloads are separate declarations under
            // one name, and which of them a reader means is the question a hover is being asked
            var functions = FindFunctionDeclarations(name);
            if (functions != null)
                return functions;

            foreach (var curr in _parser.Nodes)
            {
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
        /// Every function written under a name, whether the script defines it or a 'declare' block
        /// merely promises it, one signature per line. The name is taken to be declared at the
        /// first of them, since a definition can only point at one place.
        /// </summary>
        private System.Tuple<SymbolKind, string, TextSpan?> FindFunctionDeclarations(string name)
        {
            var signatures = new List<string>();
            var declaration = (TextSpan?) null;

            foreach (var curr in _parser.Nodes)
            {
                if (curr is FunctionNode fun && fun.Name == name)
                {
                    signatures.Add(Describe(fun));
                    declaration = declaration ?? SpanOf(fun.NameLocation);
                }

                if (!(curr is DeclarationBlockNode block))
                    continue;

                foreach (var entry in block.Entries)
                {
                    if (!(entry is DeclaredFunction declared) || declared.Name != name)
                        continue;

                    signatures.Add(Describe(declared));
                    declaration = declaration ?? SpanOf(entry);
                }
            }

            return signatures.Count == 0
                ? null
                : System.Tuple.Create(SymbolKind.Function, string.Join("\n", signatures), declaration);
        }

        /// <summary>
        /// What a 'declare' block says about a name. Functions are not looked up here - they are
        /// gathered across every block at once, so that overloads are described together.
        /// </summary>
        private static System.Tuple<SymbolKind, string, TextSpan?> FindDeclaredEntry(DeclarationBlockNode block, string name)
        {
            foreach (var curr in block.Entries)
            {
                if (curr is DeclaredProperty property && property.Name == name)
                    return System.Tuple.Create(SymbolKind.GlobalVariable, $"{(property.IsMutable ? "var" : "let")} {name} : {property.Type}", (TextSpan?) SpanOf(curr));

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

        /// <summary>
        /// How a type can be constructed, one line per overload, written the way the call is:
        /// 'new Point (X:int Y:int)'.
        /// </summary>
        private string DescribeConstructors(Resolver.TypeEntry type)
        {
            if (ReferenceEquals(type, null))
                return null;

            var declared = _context.DeclarationOf(type);
            if (declared != null && !declared.IsImported)
                return DescribeRecordConstructor(declared, type);

            try
            {
                var raw = ReflectableForm(type);
                if (raw == null)
                    return null;

                var arguments = raw.IsGenericTypeDefinition && !type.IsGenericTypeDefinition
                    ? TypeNames.ArgumentsOf(type)
                    : null;

                var signatures = raw.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                                    .Select(x => x.GetParameters())
                                    // a pointer is not something a script can hand over, and String
                                    // offers four constructors that take one
                                    .Where(x => x.All(p => !p.ParameterType.IsPointer))
                                    .Select(x => Compose(type, x.Select(p => $"{p.Name}:{TypeNames.Of(p.ParameterType, arguments)}")))
                                    .ToArray();

                // the parameterless constructor of a value type is not one reflection reports, and
                // LENS writes it out in full all the same
                if (signatures.Length == 0)
                    return raw.IsValueType ? Compose(type, Enumerable.Empty<string>()) : null;

                return string.Join("\n", signatures);
            }
            catch (Exception)
            {
                // a type still under construction answers nothing about itself
                return null;
            }
        }

        /// <summary>
        /// How a record is constructed: from its fields, in the order they are declared.
        ///
        /// Not from the constructor the compiler generated for it, although that is what the call
        /// reaches - it names its arguments in a spelling nobody wrote ('_x' for a field 'X'), and
        /// the whole use of the signature here is saying which field each argument fills.
        /// </summary>
        private string DescribeRecordConstructor(TypeEntity declared, Resolver.TypeEntry type)
        {
            var record = _parser.Nodes.OfType<RecordDefinitionNode>().FirstOrDefault(x => x.Name == declared.Name);
            if (record == null)
                return null;

            try
            {
                var fields = record.Entries.Select(
                    x => $"{x.Name}:{(declared.HasField(x.Name) ? TypeName(_context.MemberTypeOf(type, declared.ResolveField(x.Name).Type)) : x.Type.FullSignature)}"
                );

                return Compose(type, fields);
            }
            catch (Exception)
            {
                // a record whose fields did not resolve has no signature worth showing
                return null;
            }
        }

        /// <summary>
        /// Joins a type and its constructor arguments into the call that would reach it. The
        /// brackets are always written: LENS demands them even where there is nothing to pass.
        /// </summary>
        private static string Compose(Resolver.TypeEntry type, IEnumerable<string> arguments)
        {
            return $"new {TypeName(type)} ({string.Join(" ", arguments)})";
        }

        /// <summary>
        /// What a member of a type is, with one line per overload when it is a method.
        /// </summary>
        private string DescribeMember(Resolver.TypeEntry type, bool isStatic, string name)
        {
            if (ReferenceEquals(type, null))
                return null;

            // a type the script declares answers about itself: it has no CLR form to reflect over
            // until the assembly is emitted, and an analysis run emits nothing
            var declared = _context.DeclarationOf(type);
            if (declared != null && !declared.IsImported)
                return isStatic ? null : DescribeDeclaredMember(declared, type, name);

            return DescribeReflectedMember(type, isStatic, name)
                   // an extension method is called on a value, never on a type name
                   ?? (isStatic ? null : DescribeExtensionMethod(type, name));
        }

        /// <summary>
        /// A member of a record or an algebraic type, written in the terms of the reference it is
        /// reached through rather than of the parameters its declaration named.
        /// </summary>
        private string DescribeDeclaredMember(TypeEntity declared, Resolver.TypeEntry type, string name)
        {
            var field = declared.Fields.FirstOrDefault(x => x.Name == name);
            if (field != null)
                return $"{TypeName(type)}.{name} : {TypeName(_context.MemberTypeOf(type, field.Type))}";

            var methods = declared.Methods.Where(x => x.Name == name).Select(x => Describe(x, type)).ToArray();

            return methods.Length == 0 ? null : string.Join("\n", methods);
        }

        /// <summary>
        /// A member of a .NET type, as reflection reports it.
        /// </summary>
        private string DescribeReflectedMember(Resolver.TypeEntry type, bool isStatic, string name)
        {
            // LENS keeps the two apart: '.' reaches an instance member of a value, '::' a static
            // member of a type, so answering about the other one answers about what will not compile
            var flags = BindingFlags.Public
                        | BindingFlags.FlattenHierarchy
                        | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            try
            {
                var raw = ReflectableForm(type);
                if (raw == null)
                    return null;

                // when the members come off the definition rather than off the instantiation, they
                // are written in terms of its parameters, and the reader is looking at a List<int>
                var arguments = raw.IsGenericTypeDefinition && !type.IsGenericTypeDefinition
                    ? TypeNames.ArgumentsOf(type)
                    : null;

                // by name rather than by lookup: an overridden property or a hidden field is
                // reported twice by a flattened search, and GetProperty answers that with a throw
                var property = raw.GetProperties(flags).FirstOrDefault(x => x.Name == name);
                if (property != null)
                    return $"{TypeName(type)}.{name} : {TypeNames.Of(property.PropertyType, arguments)}";

                var field = raw.GetFields(flags).FirstOrDefault(x => x.Name == name);
                if (field != null)
                    return $"{TypeName(type)}.{name} : {TypeNames.Of(field.FieldType, arguments)}";

                // property accessors and operators are reached by other syntax, not by this name
                var methods = raw.GetMethods(flags)
                                 .Where(x => x.Name == name && !x.IsSpecialName)
                                 .Select(x => Describe(x, arguments))
                                 .ToArray();

                return methods.Length == 0 ? null : string.Join("\n", methods);
            }
            catch (Exception)
            {
                // a type still under construction answers nothing about itself, and a tooltip that
                // says only the name beats one that stops the editor
                return null;
            }
        }

        /// <summary>
        /// Every extension method in scope that a type could be passed to under a name.
        /// </summary>
        private string DescribeExtensionMethod(Resolver.TypeEntry type, string name)
        {
            Dictionary<string, List<MethodInfo>> methods;

            try
            {
                methods = _context.ExtensionMethodsOf(type);
            }
            catch (Exception)
            {
                return null;
            }

            return methods.TryGetValue(name, out var group)
                ? string.Join("\n", group.Select(x => DescribeExtension(x, type)))
                : null;
        }

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
            foreach (var curr in Lexems)
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
