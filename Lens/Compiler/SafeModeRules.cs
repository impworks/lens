using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Compiler
{
    /// <summary>
    /// The safe mode restrictions of one compilation, compiled into a form that can be asked about
    /// a name cheaply.
    ///
    /// The rules come in two layers. The outer one is what the host asked for - the namespaces,
    /// types and members it named, read as a blacklist or as a whitelist depending on
    /// <see cref="SafeMode"/>. The inner one is the core layer, which applies in every safe mode
    /// and which the host cannot lift: it closes the three doors that make any outer layer
    /// pointless. See <see cref="CoreDeniedNamespaces"/> for what those are and why.
    ///
    /// Matching is done on names rather than on <see cref="Type"/> objects, because the analyser
    /// asks this question about every exported type of every referenced assembly to build a
    /// completion list, and because a type the script declared has no <see cref="Type"/> yet.
    /// </summary>
    internal sealed class SafeModeRules
    {
        #region Constructor

        private SafeModeRules(SafeMode mode)
        {
            Mode = mode;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The namespaces the host named, matched against a namespace and all of its ancestors.
        /// </summary>
        private readonly HashSet<string> _namespaces = NewSet();

        /// <summary>
        /// The types the host named, by normalised full name.
        /// </summary>
        private readonly HashSet<string> _types = NewSet();

        /// <summary>
        /// The members the host named, as 'Namespace.Type::Member'.
        ///
        /// Unlike the two above, this set is a deny list in both modes. A whitelist of members
        /// would mean naming every method of every allowed type, which no host would get right;
        /// so a member rule subtracts from whatever the type-level rules allowed, and never adds.
        /// </summary>
        private readonly HashSet<string> _members = NewSet();

        #endregion

        #region Properties

        /// <summary>
        /// How the host's own rules are to be read.
        /// </summary>
        public SafeMode Mode { get; }

        #endregion

        #region Core rules

        /// <summary>
        /// Namespaces no script may name while any safe mode is on, whatever the host said.
        ///
        /// Each of these is a way around every other rule in the file rather than a capability of
        /// its own:
        ///
        /// - 'Lens' is the compiler running the script. A script that can reach
        ///   <see cref="LensCompiler"/> constructs a second one with the default options - which
        ///   means no safe mode at all - and runs whatever it likes through it.
        /// - 'System.Reflection' turns a name into an invocation. Nothing needs to be nameable for
        ///   Assembly.Load or MethodBase.Invoke to reach it, so a type-level rule cannot see it
        ///   coming.
        /// - 'System.Runtime.Loader' is the modern spelling of the same thing.
        /// - 'System.Runtime.InteropServices' hands out raw memory and turns pointers into
        ///   delegates, which ends the conversation about which types are allowed.
        ///
        /// This is why safe mode denies reflection outright rather than leaving it to the host: a
        /// blacklist that does not close this door is not a restriction, only a suggestion.
        /// </summary>
        private static readonly string[] CoreDeniedNamespaces =
        {
            "Lens",
            "System.Reflection",
            "System.Runtime.Loader",
            "System.Runtime.InteropServices"
        };

        /// <summary>
        /// Types no script may name while any safe mode is on.
        ///
        /// Activator and the AppDomain pair reach code by name the way reflection does, and the
        /// handle types are the currency reflection is spent in.
        /// </summary>
        private static readonly string[] CoreDeniedTypes =
        {
            "System.Activator",
            "System.AppDomain",
            "System.AppDomainManager",
            "System.Delegate",
            "System.MulticastDelegate",
            "System.RuntimeTypeHandle",
            "System.RuntimeMethodHandle",
            "System.RuntimeFieldHandle",
            "System.TypedReference"
        };

        /// <summary>
        /// Members no script may name while any safe mode is on.
        ///
        /// System.Type itself stays available - 'typeof' and 'is' are worth having, and a Type on
        /// its own does nothing - but the two members that turn one into code do not. Type.GetType
        /// in particular is the whole escape in one call: it takes a string, so no rule about which
        /// types may be named ever sees the type it produces.
        /// </summary>
        private static readonly string[] CoreDeniedMembers =
        {
            "System.Type::GetType",
            "System.Type::InvokeMember",
            "System.Type::ReflectionOnlyGetType"
        };

        /// <summary>
        /// Namespaces that are allowed however the rules read, because the generated code needs
        /// them and no host would think to whitelist them.
        /// </summary>
        private static readonly string[] CoreAllowedNamespaces =
        {
            "Lens.Stdlib"
        };

        /// <summary>
        /// Types that are allowed however the rules read.
        ///
        /// These are the compiler's own runtime support types. They surface as the type of an
        /// ordinary expression - unit is the type of every statement that returns nothing, and a
        /// lambda whose argument types are not written out is a Lambda&lt;...&gt; over
        /// UnspecifiedType until the call it is passed to says what they are - so a whitelist that
        /// did not include them would refuse the first line of every script.
        ///
        /// The names are spelled without the arity suffix, which is how the whole Lambda family is
        /// covered by a single entry: see <see cref="NormalizeTypeName"/>.
        /// </summary>
        private static readonly string[] CoreAllowedTypes =
        {
            "Lens.Compiler.UnitType",
            "Lens.Compiler.NullType",
            "Lens.Compiler.UnspecifiedType",
            "Lens.Compiler.Lambda"
        };

        #endregion

        #region Construction

        /// <summary>
        /// Compiles the rules of a compilation out of the options it was given.
        /// </summary>
        public static SafeModeRules From(LensCompilerOptions options)
        {
            var rules = new SafeModeRules(options.SafeMode);
            if (options.SafeMode == SafeMode.Disabled)
                return rules;

            rules.AddNamespaces(options.SafeModeExplicitNamespaces);
            rules.AddTypes(options.SafeModeExplicitTypes);
            rules.AddMembers(options.SafeModeExplicitMembers);
            rules.AddSubsystems(options.SafeModeExplicitSubsystems);

            return rules;
        }

        private void AddSubsystems(SafeModeSubsystem subsystems)
        {
            if (subsystems.HasFlag(SafeModeSubsystem.Environment))
            {
                AddNamespaces(new[] {"System.Diagnostics"});
                AddTypes(new[]
                {
                    "System.Environment",
                    "System.OperatingSystem",
                    "System.GC",
                    "System.AppContext",
                    "System.AppDomain",
                    "System.AppDomainManager"
                });
            }

            if (subsystems.HasFlag(SafeModeSubsystem.IO))
            {
                AddNamespaces(new[] {"System.IO"});
                AddTypes(new[] {"System.Console"});
            }

            if (subsystems.HasFlag(SafeModeSubsystem.Threading))
            {
                AddNamespaces(new[] {"System.Threading"});
            }

            if (subsystems.HasFlag(SafeModeSubsystem.Reflection))
            {
                AddNamespaces(new[] {"System.Reflection", "System.Runtime.Loader"});
                AddTypes(new[]
                {
                    "System.Type",
                    "System.Activator",
                    "System.AppDomain",
                    "System.AppDomainManager"
                });

                // a script that may not name System.Type can still be handed one
                AddMembers(new[] {"System.Object::GetType"});
            }

            if (subsystems.HasFlag(SafeModeSubsystem.Network))
            {
                AddNamespaces(new[] {"System.Net", "System.Web"});
            }
        }

        private void AddNamespaces(IEnumerable<string> namespaces)
        {
            foreach (var curr in Clean(namespaces))
                _namespaces.Add(curr);
        }

        private void AddTypes(IEnumerable<string> types)
        {
            foreach (var curr in Clean(types))
                _types.Add(NormalizeTypeName(curr));
        }

        private void AddMembers(IEnumerable<string> members)
        {
            foreach (var curr in Clean(members))
            {
                var separator = curr.IndexOf("::", StringComparison.Ordinal);
                if (separator <= 0 || separator == curr.Length - 2)
                    throw new ArgumentException($"'{curr}' is not a member rule: the expected form is 'Namespace.Type::Member'.");

                var type = NormalizeTypeName(curr.Substring(0, separator).Trim());
                var member = curr.Substring(separator + 2).Trim();

                _members.Add(type + "::" + member);
            }
        }

        /// <summary>
        /// Drops what a hand-written list tends to pick up: stray whitespace, and blank entries
        /// left behind by a trailing comma. A rule that silently does not match is worse here than
        /// almost anywhere else, since nothing goes wrong until something does.
        /// </summary>
        private static IEnumerable<string> Clean(IEnumerable<string> entries)
        {
            if (entries == null)
                yield break;

            foreach (var curr in entries)
            {
                if (string.IsNullOrWhiteSpace(curr))
                    continue;

                yield return curr.Trim();
            }
        }

        #endregion

        #region Type checks

        /// <summary>
        /// Whether a type with the given name and namespace may be named by the script.
        ///
        /// Both arguments may be null: a generic parameter, a type the script declared and an
        /// array of either have no name a rule could match. The caller decides what that means -
        /// see the overloads on the context - and passes what it has.
        /// </summary>
        public bool IsNameAllowed(string fullName, string nsp)
        {
            if (Mode == SafeMode.Disabled)
                return true;

            var names = EnclosingNames(fullName);

            if (Matches(names, nsp, CoreAllowedTypes, CoreAllowedNamespaces))
                return true;

            if (Matches(names, nsp, CoreDeniedTypes, CoreDeniedNamespaces))
                return false;

            var listed = MatchesHostRules(names, nsp);

            return Mode == SafeMode.Blacklist ? !listed : listed;
        }

        private bool MatchesHostRules(IEnumerable<string> names, string nsp)
        {
            foreach (var curr in names)
                if (_types.Contains(curr))
                    return true;

            return ContainsNamespace(_namespaces, nsp);
        }

        private static bool Matches(IEnumerable<string> names, string nsp, IEnumerable<string> types, IEnumerable<string> namespaces)
        {
            foreach (var curr in names)
                if (types.Contains(curr))
                    return true;

            foreach (var curr in Ancestors(nsp))
                if (namespaces.Contains(curr))
                    return true;

            return false;
        }

        #endregion

        #region Member checks

        /// <summary>
        /// Whether a member of the given name may be named on any of the given types.
        ///
        /// The caller passes the type the member is declared on together with its base types, so
        /// that a rule about Object.GetType covers every expression it can be called on. Member
        /// rules only ever deny, in both modes, so there is no whitelist branch here.
        /// </summary>
        public bool IsMemberAllowed(IEnumerable<string> declaringTypeNames, string memberName)
        {
            if (Mode == SafeMode.Disabled)
                return true;

            if (string.IsNullOrEmpty(memberName))
                return true;

            var haveRules = _members.Count > 0;

            foreach (var type in declaringTypeNames)
            {
                if (type == null)
                    continue;

                foreach (var name in EnclosingNames(type))
                {
                    var rule = name + "::" + memberName;

                    if (CoreDeniedMembers.Contains(rule))
                        return false;

                    if (haveRules && _members.Contains(rule))
                        return false;
                }
            }

            return true;
        }

        #endregion

        #region Name helpers

        /// <summary>
        /// A type name as the rules spell it: without the arity suffix the CLR adds to a generic
        /// name, and without the argument list a constructed one carries.
        ///
        /// This is what lets a host write 'System.Collections.Generic.List' and have it match
        /// List&lt;int&gt;, whose own full name is 'System.Collections.Generic.List`1[[System.Int32, ...]]'.
        /// </summary>
        private static string NormalizeTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return fullName;

            var arguments = fullName.IndexOf('[');
            if (arguments >= 0)
                fullName = fullName.Substring(0, arguments);

            var arity = fullName.IndexOf('`');
            while (arity >= 0)
            {
                var end = arity + 1;
                while (end < fullName.Length && char.IsDigit(fullName[end]))
                    end++;

                fullName = fullName.Substring(0, arity) + fullName.Substring(end);
                arity = fullName.IndexOf('`', arity);
            }

            return fullName.TrimEnd('&', '*');
        }

        /// <summary>
        /// The normalised name of a type together with the names of the types it is nested in, so
        /// that a rule about an outer type covers what is declared inside it.
        /// </summary>
        private static IEnumerable<string> EnclosingNames(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return NoNames;

            var name = NormalizeTypeName(fullName);
            if (name.IndexOf('+') < 0)
                return new[] {name};

            var result = new List<string>();
            var offset = -1;

            while (true)
            {
                offset = name.IndexOf('+', offset + 1);
                if (offset < 0)
                    break;

                result.Add(name.Substring(0, offset));
            }

            result.Add(name);

            return result;
        }

        /// <summary>
        /// A namespace and every namespace it sits in, innermost first.
        ///
        /// A rule naming 'System.Net' has to cover 'System.Net.Sockets' and must not cover a
        /// namespace that merely starts with the same letters. Walking the dots is how that
        /// boundary is kept: the old check was a StringStartsWith, which said yes to
        /// 'System.Text' for a rule about 'System.Te'.
        /// </summary>
        private static IEnumerable<string> Ancestors(string nsp)
        {
            if (string.IsNullOrEmpty(nsp))
                yield break;

            yield return nsp;

            for (var i = nsp.Length - 1; i > 0; i--)
                if (nsp[i] == '.')
                    yield return nsp.Substring(0, i);
        }

        private static bool ContainsNamespace(HashSet<string> namespaces, string nsp)
        {
            if (namespaces.Count == 0)
                return false;

            foreach (var curr in Ancestors(nsp))
                if (namespaces.Contains(curr))
                    return true;

            return false;
        }

        private static HashSet<string> NewSet()
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        private static readonly string[] NoNames = new string[0];

        #endregion
    }
}
