using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            var receiver = FindReceiver(position);

            return receiver == null
                ? CompleteNames(position)
                : CompleteMembers(receiver.Type, receiver.IsStatic);
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

                target.Add(new CompletionEntry(curr.Key, SymbolKind.Member, Describe(curr.Value[0])));
            }
        }

        #endregion

        #region Receiver resolution

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

            using (var variant = _analyzer.AnalyzeVariant(patched))
                return variant.TypeEndingAt(end, preferWidest);
        }

        /// <summary>
        /// Reads a patched version of the file and asks it what a position is inside of.
        /// </summary>
        private TypeEntry TypeCoveringInVariant(string patched, LexemLocation position)
        {
            if (patched == null)
                return null;

            using (var variant = _analyzer.AnalyzeVariant(patched))
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

        private static string Describe(MethodInfo method, IDictionary<string, string> arguments = null)
        {
            var args = string.Join(" ", method.GetParameters().Select(x => $"{x.Name}:{TypeNames.Of(x.ParameterType, arguments)}"));
            return $"{method.Name}:{TypeNames.Of(method.ReturnType, arguments)}{(args.Length > 0 ? " (" + args + ")" : "")}";
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
