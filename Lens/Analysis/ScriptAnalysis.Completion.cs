using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree;

namespace Lens.Analysis
{
    /// <summary>
    /// What may be written at a point in the file.
    ///
    /// Two questions, not one. After a '.' the answer is the members of whatever is to the left,
    /// which needs the receiver's bound type. Anywhere else it is every name in scope, which needs
    /// the scope chain. The first is the harder one, because 'foo.' does not parse: the text is
    /// analysed a second time with the dot and the partial name blanked out, so that 'foo' is a
    /// complete expression again and binding can say what it is.
    /// </summary>
    public sealed partial class ScriptAnalysis
    {
        #region Completion

        /// <summary>
        /// The names that may be written at a position.
        /// </summary>
        public IReadOnlyList<CompletionEntry> Complete(LexemLocation position)
        {
            // a 'use' directive names a namespace rather than an expression, and its dots separate
            // namespace segments rather than reaching a member of anything
            var namespacePrefix = FindNamespacePrefix(position);
            if (namespacePrefix != null)
                return CompleteNamespaces(namespacePrefix);

            var receiver = FindReceiver(position);
            if (receiver != null)
                return CompleteMembers(receiver.Type, receiver.IsStatic);

            // 'new' is followed by a type and by nothing else, so the names in scope are the wrong
            // answer there however many of them there are
            var newPrefix = FindNewPrefix(position);

            return newPrefix == null
                ? CompleteNames(position)
                : CompleteTypes(newPrefix);
        }

        /// <summary>
        /// The namespaces that may follow what has been typed of a 'use' directive so far.
        /// </summary>
        private IReadOnlyList<CompletionEntry> CompleteNamespaces(string prefix)
        {
            var result = new List<CompletionEntry>();

            foreach (var curr in _context.NamespacesUnder(prefix))
                result.Add(new CompletionEntry(curr, SymbolKind.Namespace, prefix.Length == 0 ? curr : prefix + "." + curr));

            return result;
        }

        /// <summary>
        /// The types that may follow a 'new'.
        ///
        /// Nothing but a type may: the names in scope are all wrong there, and there are enough of
        /// them to bury the one word that would have compiled.
        /// </summary>
        private IReadOnlyList<CompletionEntry> CompleteTypes(string prefix)
        {
            var result = new List<CompletionEntry>();
            var seen = new HashSet<string>();

            // what the script declares goes first: there is a handful of those against thousands of
            // host types, and a record is what a script constructs most of the time
            if (prefix.Length == 0)
            {
                foreach (var curr in _context.DefinedTypes)
                {
                    if (curr.Key.StartsWith("<") || !seen.Add(curr.Key))
                        continue;

                    result.Add(
                        new CompletionEntry(
                            curr.Key,
                            curr.Value.IsImported ? SymbolKind.HostType : SymbolKind.Record,
                            curr.Value.TypeInfo?.FullName ?? curr.Key
                        )
                    );
                }

                // the language's own names for host types: 'string' is what a script writes, and no
                // namespace leads to it - System holds a 'String'
                foreach (var curr in TypeResolver.Aliases)
                {
                    if (seen.Add(curr.Key))
                        result.Add(new CompletionEntry(curr.Key, SymbolKind.HostType, curr.Value.FullName ?? curr.Key));
                }
            }

            foreach (var curr in prefix.Length == 0 ? _context.TypesInScope() : _context.TypesInNamespace(prefix))
            {
                if (!IsConstructible(curr))
                    continue;

                var name = TypeNames.ShortNameOf(curr);
                if (!seen.Add(name))
                    continue;

                result.Add(new CompletionEntry(name, SymbolKind.HostType, TypeNames.SignatureOf(curr)));
            }

            // a type may also be reached by spelling out where it lives, so the namespaces under
            // what has been typed so far belong in the list beside the types themselves
            foreach (var curr in _context.NamespacesUnder(prefix))
            {
                if (seen.Add(curr))
                    result.Add(new CompletionEntry(curr, SymbolKind.Namespace, prefix.Length == 0 ? curr : prefix + "." + curr));
            }

            return result;
        }

        /// <summary>
        /// Whether 'new' could be written in front of a type. An interface or an abstract class has
        /// no instances, and an enum has all of its own already.
        /// </summary>
        private static bool IsConstructible(Type type)
        {
            return !type.IsAbstract
                   && !type.IsInterface
                   && !type.IsEnum
                   && !type.IsSpecialName
                   && type.Name.IndexOf('<') < 0;
        }

        /// <summary>
        /// Everything visible at a position: locals in scope, then the environment.
        /// </summary>
        private IReadOnlyList<CompletionEntry> CompleteNames(LexemLocation position)
        {
            var result = new List<CompletionEntry>();
            var seen = new HashSet<string>();

            foreach (var scope in _context.ScopesAt(position))
            {
                foreach (var local in scope.Locals.Values)
                {
                    if (local.Name.StartsWith("<") || !seen.Add(local.Name))
                        continue;

                    result.Add(
                        new CompletionEntry(
                            local.Name,
                            local.ArgumentId != null ? SymbolKind.Parameter : SymbolKind.Local,
                            TypeName(local.Type)
                        )
                    );
                }
            }

            foreach (var curr in _context.DefinedProperties)
            {
                if (seen.Add(curr.Key))
                    result.Add(new CompletionEntry(curr.Key, SymbolKind.GlobalVariable, TypeNames.Of(curr.Value.PropertyType)));
            }

            foreach (var curr in _context.MainType.Methods)
            {
                if (curr.Name.StartsWith("<") || !seen.Add(curr.Name))
                    continue;

                result.Add(new CompletionEntry(curr.Name, SymbolKind.Function, Describe(curr)));
            }

            foreach (var curr in _context.DefinedTypes)
            {
                if (curr.Key.StartsWith("<") || !seen.Add(curr.Key))
                    continue;

                result.Add(new CompletionEntry(curr.Key, SymbolKind.HostType, curr.Value.TypeInfo?.FullName ?? curr.Key));
            }

            foreach (var curr in Keywords)
            {
                if (seen.Add(curr))
                    result.Add(new CompletionEntry(curr, SymbolKind.Keyword, "keyword"));
            }

            return result;
        }

        /// <summary>
        /// The members of a type, extension methods included - a LENS script spends most of its
        /// time in LINQ, and an editor that does not offer Select is not offering much.
        /// </summary>
        private IReadOnlyList<CompletionEntry> CompleteMembers(TypeEntry type, bool isStatic)
        {
            var result = new List<CompletionEntry>();
            var seen = new HashSet<string>();

            // by declaration rather than by name: a record's type can arrive here as any of several
            // entries over the same declaration, and only the declaration has members to list. An
            // instantiation of a generic one is not named after its declaration at all - Foo<int>
            // is reached through the entry it was constructed from.
            var declared = _context.DeclarationOf(type);
            if (declared != null && !declared.IsImported)
            {
                if (isStatic)
                    return result;

                foreach (var curr in declared.Fields)
                    if (seen.Add(curr.Name))
                        result.Add(new CompletionEntry(curr.Name, SymbolKind.RecordField, TypeName(_context.MemberTypeOf(type, curr.Type))));

                foreach (var curr in declared.Methods)
                    if (!curr.Name.StartsWith("<") && seen.Add(curr.Name))
                        result.Add(new CompletionEntry(curr.Name, SymbolKind.Member, Describe(curr, type)));

                return result;
            }

            AddReflectedMembers(result, seen, type, isStatic);

            // an extension method is called on a value, never on a type name
            if (!isStatic)
                AddExtensionMethods(result, seen, type);

            return result;
        }

        /// <summary>
        /// The public surface of a .NET type.
        /// </summary>
        private void AddReflectedMembers(List<CompletionEntry> target, HashSet<string> seen, TypeEntry type, bool isStatic)
        {
            // LENS keeps the two apart: '.' reaches an instance member of a value, '::' a static
            // member of a type, so offering both under either would be offering what will not compile
            var flags = BindingFlags.Public
                        | BindingFlags.FlattenHierarchy
                        | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            try
            {
                var raw = ReflectableForm(type);
                if (raw == null)
                    return;

                // when the members come off the definition rather than off the instantiation, they
                // are written in terms of its parameters, and the caller asked about List<int>
                var arguments = raw.IsGenericTypeDefinition && !type.IsGenericTypeDefinition
                    ? TypeNames.ArgumentsOf(type)
                    : null;

                foreach (var curr in raw.GetProperties(flags))
                    if (seen.Add(curr.Name))
                        target.Add(new CompletionEntry(curr.Name, SymbolKind.Member, TypeNames.Of(curr.PropertyType, arguments)));

                foreach (var curr in raw.GetFields(flags))
                    if (seen.Add(curr.Name))
                        target.Add(new CompletionEntry(curr.Name, SymbolKind.Member, TypeNames.Of(curr.FieldType, arguments)));

                foreach (var curr in raw.GetMethods(flags))
                {
                    // property accessors and operators are reached by other syntax, not by name
                    if (curr.IsSpecialName || !seen.Add(curr.Name))
                        continue;

                    target.Add(new CompletionEntry(curr.Name, SymbolKind.Member, Describe(curr, arguments)));
                }
            }
            catch (Exception)
            {
                // a type still under construction answers nothing about itself, and an editor
                // offering no members beats one that stops working
            }
        }

        /// <summary>
        /// The CLR type whose members answer for an entry.
        ///
        /// An instantiation that mentions a declaration - List&lt;T&gt; inside a generic function, or
        /// List&lt;SomeRecord&gt; - has no CLR type until the assembly is emitted, and an analysis run
        /// emits nothing. Its definition does, and carries the same members under the same names,
        /// which is what a completion list is made of.
        /// </summary>
        private static Type ReflectableForm(TypeEntry type)
        {
            if (!type.ContainsDeclared)
                return type.Materialize();

            var definition = type.GetGenericDefinition();

            return definition == null || definition.ContainsDeclared
                ? null
                : definition.Materialize();
        }

        /// <summary>
        /// The extension methods in scope for a type.
        /// </summary>
        private void AddExtensionMethods(List<CompletionEntry> target, HashSet<string> seen, TypeEntry type)
        {
            Dictionary<string, List<MethodInfo>> methods;

            try
            {
                methods = _context.ExtensionMethodsOf(type);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var curr in methods)
            {
                if (!seen.Add(curr.Key))
                    continue;

                target.Add(new CompletionEntry(curr.Key, SymbolKind.Member, DescribeExtension(curr.Value[0], type)));
            }
        }

        #endregion

        #region Receiver resolution

        /// <summary>
        /// A 'use' directive, with the namespace it names split into the part that is complete and
        /// the part still being typed.
        /// </summary>
        private static readonly Regex UseDirective = new Regex(
            @"^\s*use\s+(?<prefix>[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*\.)?[A-Za-z0-9_]*$",
            RegexOptions.Compiled
        );

        /// <summary>
        /// The namespace whose children the caret is completing, "" for the roots, or null when the
        /// caret is not in a 'use' directive at all.
        ///
        /// Read off the text rather than off the tree, because there is no tree to read: a directive
        /// that ends in a dot does not parse, and one namespace segment is not a node in any case -
        /// the whole of 'use System.Linq' binds to a single name.
        /// </summary>
        private string FindNamespacePrefix(LexemLocation position)
        {
            var caret = IndexOf(position);
            if (caret < 0)
                return null;

            var start = caret;
            while (start > 0 && Source[start - 1] != '\n')
                start--;

            var match = UseDirective.Match(Source.Substring(start, caret - start));
            if (!match.Success)
                return null;

            // the prefix is captured with the dot that separates it from what is being typed
            var prefix = match.Groups["prefix"].Value;
            return prefix.Length == 0 ? "" : prefix.Substring(0, prefix.Length - 1);
        }

        /// <summary>
        /// A 'new' and the type name being written after it, split into the namespace that has been
        /// spelled out and the part still being typed.
        /// </summary>
        private static readonly Regex NewExpression = new Regex(
            @"(^|[^A-Za-z0-9_])new\s+(?<prefix>[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*\.)?[A-Za-z0-9_]*$",
            RegexOptions.Compiled
        );

        /// <summary>
        /// The namespace the type name after a 'new' is being written under, "" when the name is
        /// unqualified, or null when the caret is not naming a constructed type at all.
        ///
        /// Read off the text for the same reason a 'use' directive is: 'new ' does not parse, and
        /// what follows it is a type signature rather than a node the tree would hold. The trailing
        /// space matters - 'new' with the caret against it is still a name being typed, and the
        /// other collection literals ('new [', 'new (') never reach the pattern because a bracket
        /// is not part of a name.
        /// </summary>
        private string FindNewPrefix(LexemLocation position)
        {
            var caret = IndexOf(position);
            if (caret < 0)
                return null;

            var start = caret;
            while (start > 0 && Source[start - 1] != '\n')
                start--;

            var match = NewExpression.Match(Source.Substring(start, caret - start));
            if (!match.Success)
                return null;

            // the prefix is captured with the dot that separates it from what is being typed
            var prefix = match.Groups["prefix"].Value;
            return prefix.Length == 0 ? "" : prefix.Substring(0, prefix.Length - 1);
        }

        /// <summary>
        /// What the caret is completing a member of, or null when it is not completing one at all.
        /// </summary>
        private Receiver FindReceiver(LexemLocation position)
        {
            var caret = IndexOf(position);
            if (caret < 0)
                return null;

            // step back over whatever has been typed of the member name so far
            var index = caret - 1;
            while (index >= 0 && IsNameChar(Source[index]))
                index--;

            if (index < 0)
                return null;

            if (Source[index] == ':' && index > 0 && Source[index - 1] == ':')
                return FindStaticReceiver(index - 1);

            if (Source[index] == '.')
                return FindInstanceReceiver(caret, index);

            // a pipe reads a member of what is to its left exactly as a dot does - 'xs |> Select f'
            // is 'xs.Select f' - but it is written apart from it, so the blanks in between are
            // stepped over before the operator itself can be recognised
            var pipe = index;
            while (pipe >= 0 && (Source[pipe] == ' ' || Source[pipe] == '\t'))
                pipe--;

            return pipe > 0 && Source[pipe] == '>' && Source[pipe - 1] == '|'
                ? FindPipeReceiver(caret, pipe - 1)
                : null;
        }

        /// <summary>
        /// The type named to the left of a '::'.
        ///
        /// No second reading is needed here: the left of a '::' is a type name rather than an
        /// expression, so it can be resolved as written.
        /// </summary>
        private Receiver FindStaticReceiver(int colons)
        {
            var start = colons;
            while (start > 0 && (IsNameChar(Source[start - 1]) || Source[start - 1] == '.'))
                start--;

            var name = Source.Substring(start, colons - start).Trim();
            if (name.Length == 0)
                return null;

            try
            {
                var type = _context.ResolveType(name);

                return ReferenceEquals(type, null)
                    ? null
                    : new Receiver {Type = type, IsStatic = true};
            }
            catch (Exception)
            {
                // not a type, or not one this script can see
                return null;
            }
        }

        /// <summary>
        /// The type of the expression to the left of a '.'.
        ///
        /// 'foo.' does not parse, so the file is read a second time with the accessor blanked out.
        /// Every position before the dot is untouched, so the answer is about the code as written.
        /// </summary>
        private Receiver FindInstanceReceiver(int caret, int dot)
        {
            var receiverEnd = dot;
            if (receiverEnd > 0 && Source[receiverEnd - 1] == '?')
                receiverEnd--;

            if (receiverEnd == 0)
                return null;

            var end = LocationOf(receiverEnd);

            // what the dot binds to is decided by the character before it. A bracket closes a whole
            // expression, and the access applies to all of it - '(a + b).', 'xs.Select(f).'. A name
            // binds only itself, however much happens to end there: the body of 'x -> x.' is an
            // identifier, not the lambda, and 'f x.' passes f a member of x rather than reading a
            // member of the call.
            var closed = Source[receiverEnd - 1];
            var preferWidest = closed == ')' || closed == ']' || closed == '}';

            // blanking the accessor alone keeps whatever follows it on the line, which is what an
            // expression the caret sits inside of needs: 'new [ x. ]' loses its closing bracket -
            // and with it the whole statement - if the rest of the line goes as well. The wider
            // blanking is still tried afterwards, for a tail that does not survive on its own.
            var type = TypeOfVariant(BlankAccessor(receiverEnd, caret), end, preferWidest)
                       ?? TypeOfVariant(BlankLineTail(receiverEnd), end, preferWidest);

            return ReferenceEquals(type, null)
                ? null
                : new Receiver {Type = type, IsStatic = false};
        }

        /// <summary>
        /// The type of the expression to the left of a '|&gt;'.
        ///
        /// The whole expression is piped, however much of it there is and however many lines back
        /// it starts: unlike a dot, the operator is not part of what precedes it, so the member is
        /// read from the outermost expression the text before it belongs to.
        /// </summary>
        private Receiver FindPipeReceiver(int caret, int pipe)
        {
            var receiverEnd = pipe;
            while (receiverEnd > 0 && char.IsWhiteSpace(Source[receiverEnd - 1]))
                receiverEnd--;

            if (receiverEnd == 0)
                return null;

            var end = LocationOf(receiverEnd - 1);

            var type = TypeCoveringInVariant(BlankAccessor(pipe, caret), end)
                       ?? TypeCoveringInVariant(BlankLineTail(pipe), end);

            return ReferenceEquals(type, null)
                ? null
                : new Receiver {Type = type, IsStatic = false};
        }

        /// <summary>
        /// Reads a patched version of the file and asks it what ends at a position.
        /// </summary>
        private TypeEntry TypeOfVariant(string patched, LexemLocation end, bool preferWidest)
        {
            if (patched == null)
                return null;

            using (var variant = _analyzer.AnalyzeVariant(patched, _baseDirectory))
                return variant.TypeEndingAt(end, preferWidest);
        }

        /// <summary>
        /// Reads a patched version of the file and asks it what a position is inside of.
        /// </summary>
        private TypeEntry TypeCoveringInVariant(string patched, LexemLocation position)
        {
            if (patched == null)
                return null;

            using (var variant = _analyzer.AnalyzeVariant(patched, _baseDirectory))
                return variant.TypeCovering(position);
        }

        /// <summary>
        /// Whether a character can be part of a name.
        /// </summary>
        private static bool IsNameChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        /// <summary>
        /// What a member is being completed on, and how it is being reached.
        /// </summary>
        private sealed class Receiver
        {
            public TypeEntry Type;
            public bool IsStatic;
        }

        /// <summary>
        /// Replaces the accessor being completed - the '.', and the part of the member name that
        /// has been typed so far - with spaces, leaving the rest of the line where it was.
        /// </summary>
        private string BlankAccessor(int from, int caret)
        {
            var end = caret;
            while (end < Source.Length && IsNameChar(Source[end]))
                end++;

            return Source.Substring(0, from) + new string(' ', end - from) + Source.Substring(end);
        }

        /// <summary>
        /// Replaces the rest of the line after a point with spaces, leaving every position before
        /// it - and every other line - exactly where it was.
        /// </summary>
        private string BlankLineTail(int from)
        {
            var end = from;
            while (end < Source.Length && Source[end] != '\r' && Source[end] != '\n')
                end++;

            return Source.Substring(0, from) + new string(' ', end - from) + Source.Substring(end);
        }

        /// <summary>
        /// The bound type of an expression that ends at a position.
        ///
        /// More than one ends there - 'f x' ends where its argument does, and 'x -> x' where its
        /// body does - so the caller says which of the two nesting levels the access binds to.
        ///
        /// Nothing ends there at all when the expression was written in parentheses - '(x)' is the
        /// node 'x' with a span that covers the brackets - so an expression that merely surrounds
        /// the position answers as well, the innermost one first.
        /// </summary>
        internal TypeEntry TypeEndingAt(LexemLocation location, bool preferWidest = true)
        {
            TypeEntry ending = null;
            var endingStart = preferWidest ? (Line: int.MaxValue, Offset: int.MaxValue) : (Line: int.MinValue, Offset: int.MinValue);

            TypeEntry around = null;
            var aroundSize = int.MaxValue;

            foreach (var curr in AllNodes())
            {
                var type = _context.FindExpressionType(curr);
                if (ReferenceEquals(type, null))
                    continue;

                // 'let z = x' ends exactly where 'x' does and is wider, but it is a statement
                // rather than a value: nothing is being completed on the absence of a value
                if (type.IsVoid())
                    continue;

                if (curr.EndLocation.Line == location.Line && curr.EndLocation.Offset == location.Offset)
                {
                    var start = (curr.StartLocation.Line, curr.StartLocation.Offset);
                    var better = preferWidest ? start.CompareTo(endingStart) < 0 : start.CompareTo(endingStart) > 0;
                    if (better)
                    {
                        ending = type;
                        endingStart = start;
                    }

                    continue;
                }

                if (!Contains(SpanOf(curr), location))
                    continue;

                var size = SpanSize(curr);
                if (size >= aroundSize)
                    continue;

                around = type;
                aroundSize = size;
            }

            return ending ?? around;
        }

        /// <summary>
        /// The bound type of the widest expression that covers a position.
        ///
        /// What a pipe applies to cannot be looked up by where its text ends: an invocation written
        /// across several lines closes on the line break after it rather than on its last argument,
        /// so 'xs |> Where f' does not end where the 'f' does. What the position is inside of
        /// answers instead, and the outermost of those is the whole expression being piped.
        /// </summary>
        internal TypeEntry TypeCovering(LexemLocation location)
        {
            TypeEntry best = null;
            var bestStart = (Line: int.MaxValue, Offset: int.MaxValue);

            foreach (var curr in AllNodes())
            {
                var type = _context.FindExpressionType(curr);
                if (ReferenceEquals(type, null) || type.IsVoid())
                    continue;

                if (!Contains(SpanOf(curr), location))
                    continue;

                // spans nest, so the one that starts earliest is the outermost of those covering
                // the position - no statement among them, those have no value to pipe
                var start = (curr.StartLocation.Line, curr.StartLocation.Offset);
                if (start.CompareTo(bestStart) >= 0)
                    continue;

                best = type;
                bestStart = start;
            }

            return best;
        }

        /// <summary>
        /// A rough measure of how much source a node covers, for picking the innermost of several.
        /// </summary>
        private static int SpanSize(NodeBase node)
        {
            var lines = node.EndLocation.Line - node.StartLocation.Line;
            return lines * 1000 + (lines == 0 ? node.EndLocation.Offset - node.StartLocation.Offset : 0);
        }

        /// <summary>
        /// Every node in the tree.
        /// </summary>
        private IEnumerable<NodeBase> AllNodes()
        {
            var pending = new Stack<NodeBase>(_parser.Nodes.Where(x => x != null));

            while (pending.Count > 0)
            {
                var curr = pending.Pop();
                yield return curr;

                foreach (var child in curr.GetChildren())
                {
                    if (child?.Node != null)
                        pending.Push(child.Node);
                }
            }
        }

        #endregion

        #region Source positions

        /// <summary>
        /// The index in the source of a line-and-offset position.
        /// </summary>
        private int IndexOf(LexemLocation position)
        {
            var starts = LineStarts;

            if (position.Line < 1 || position.Line > starts.Length)
                return -1;

            var index = starts[position.Line - 1] + position.Offset - 1;
            return index < 0 || index > Source.Length ? -1 : index;
        }

        /// <summary>
        /// The line-and-offset position of an index in the source: the inverse of IndexOf.
        /// </summary>
        private LexemLocation LocationOf(int index)
        {
            var starts = LineStarts;
            var line = 0;

            while (line + 1 < starts.Length && starts[line + 1] <= index)
                line++;

            return new LexemLocation {Line = line + 1, Offset = index - starts[line] + 1};
        }

        /// <summary>
        /// Where every line of the source begins.
        /// </summary>
        private int[] LineStarts
        {
            get
            {
                if (_lineStarts != null)
                    return _lineStarts;

                var starts = new List<int> {0};

                for (var i = 0; i < Source.Length; i++)
                {
                    if (Source[i] == '\n')
                        starts.Add(i + 1);
                }

                return _lineStarts = starts.ToArray();
            }
        }

        private int[] _lineStarts;

        #endregion

        #region Descriptions

        /// <summary>
        /// Describes a .NET method.
        /// </summary>
        /// <param name="method">The method as reflection reports it.</param>
        /// <param name="arguments">What to call the generic parameters its signature mentions.</param>
        /// <param name="skip">
        /// How many leading parameters are not written at the call site. One, for an extension
        /// method: its receiver goes to the left of the dot rather than into the brackets.
        /// </param>
        private static string Describe(MethodInfo method, IDictionary<string, string> arguments = null, int skip = 0)
        {
            var args = string.Join(" ", method.GetParameters().Skip(skip).Select(x => $"{x.Name}:{TypeNames.Of(x.ParameterType, arguments)}"));
            return $"{method.Name}:{TypeNames.Of(method.ReturnType, arguments)}{(args.Length > 0 ? " (" + args + ")" : "")}";
        }

        /// <summary>
        /// Describes an extension method as the receiver sees it.
        ///
        /// An extension method is declared over its own parameters, and reflection reports it that
        /// way: the Where of an int[] is offered as taking a Func&lt;TSource, bool&gt;, which is not
        /// a thing anybody can write. The receiver pins those parameters down, so it is asked.
        /// </summary>
        private string DescribeExtension(MethodInfo method, TypeEntry type)
        {
            return Describe(method, ExtensionArgumentsOf(method, type), 1);
        }

        /// <summary>
        /// What the generic parameters of an extension method are, once the receiver has been
        /// matched against the one parameter it is passed as. Null when it does not pin them down.
        /// </summary>
        private IDictionary<string, string> ExtensionArgumentsOf(MethodInfo method, TypeEntry type)
        {
            var parameters = method.GetGenericArguments();
            if (parameters.Length == 0)
                return null;

            try
            {
                var values = GenericHelper.ResolveMethodGenericsByArgs(
                    _context.Resolver,
                    new[] {method.GetParameters()[0].ParameterType},
                    new[] {type.Materialize()},
                    parameters
                );

                var result = new Dictionary<string, string>();

                for (var idx = 0; idx < parameters.Length && idx < values.Length; idx++)
                    result[parameters[idx].Name] = TypeNames.Of(values[idx]);

                return result;
            }
            catch (Exception)
            {
                // a parameter the receiver says nothing about cannot be named, and the name the
                // declaration gave it is still an honest answer
                return null;
            }
        }

        /// <summary>
        /// The words that are always valid to write.
        /// </summary>
        private static readonly string[] Keywords =
        {
            "declare", "use", "record", "type", "fun", "pure", "let", "var", "new", "if", "then",
            "else", "while", "do", "for", "in", "try", "catch", "finally", "throw", "match", "with",
            "case", "when", "yield", "await", "using", "not", "is", "as", "of", "ref", "typeof",
            "default", "true", "false", "null"
        };

        #endregion
    }
}
