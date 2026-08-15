using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.Resolver;

namespace Lens.Analysis
{
    /// <summary>
    /// Type names as a LENS programmer wrote them, rather than as the CLR spells them.
    ///
    /// Reflection reports an instantiation under the arity-mangled name of its definition - List`1 -
    /// and says nothing about what it was instantiated with. That is the wrong answer for anything a
    /// person reads: a tooltip saying 'List`1' tells them less than the line they are hovering over.
    /// </summary>
    internal static class TypeNames
    {
        #region Constants

        /// <summary>
        /// What to show when there is no type at all - an expression that did not bind.
        /// </summary>
        public const string Unknown = "?";

        /// <summary>
        /// The language's short names for host types, by the full name reflection reports.
        /// </summary>
        private static readonly Dictionary<string, string> AliasesByFullName =
            TypeResolver.Aliases
                        .Where(x => x.Value.FullName != null)
                        .GroupBy(x => x.Value.FullName)
                        .ToDictionary(x => x.Key, x => x.First().Key);

        #endregion

        #region Methods

        /// <summary>
        /// The name of a type as the compiler models it.
        /// </summary>
        public static string Of(TypeEntry type)
        {
            if (ReferenceEquals(type, null))
                return Unknown;

            if (type.IsArray)
                return Of(type.ElementType) + "[]";

            if (type.IsByRef)
                return "ref " + Of(type.ElementType);

            if (type.IsPointer)
                return Of(type.ElementType) + "*";

            var arguments = type.GenericArguments;
            if (arguments.Length == 0)
                return Alias(type.FullName) ?? type.Name;

            // LENS spells a nullable as 'int?', which is also how its own signatures are written
            if (arguments.Length == 1 && type.GetGenericDefinition()?.Is(typeof(Nullable<>)) == true)
                return Of(arguments[0]) + "?";

            return Compose(type.Name, arguments.Select(Of));
        }

        /// <summary>
        /// The name of a CLR type, for the members that reflection reports directly.
        /// </summary>
        /// <param name="parameters">
        /// What to call the generic parameters the type mentions, by name. Members reflected off a
        /// generic definition talk in terms of its parameters, and a reader looking at a List&lt;int&gt;
        /// wants to be told 'int' rather than 'T'.
        /// </param>
        public static string Of(Type type, IDictionary<string, string> parameters = null)
        {
            if (type == null)
                return Unknown;

            if (type.IsArray)
                return Of(type.GetElementType(), parameters) + "[]";

            if (type.IsByRef)
                return "ref " + Of(type.GetElementType(), parameters);

            if (type.IsPointer)
                return Of(type.GetElementType(), parameters) + "*";

            if (type.IsGenericParameter && parameters != null && parameters.TryGetValue(type.Name, out var substituted))
                return substituted;

            if (!type.IsGenericType)
                return Alias(type.FullName) ?? type.Name;

            var arguments = type.GetGenericArguments();

            if (arguments.Length == 1 && Nullable.GetUnderlyingType(type) != null)
                return Of(arguments[0], parameters) + "?";

            return Compose(type.Name, arguments.Select(x => Of(x, parameters)));
        }

        /// <summary>
        /// What the parameters of a generic definition are called in one of its instantiations.
        /// Empty when the type is not an instantiation, or when the two lists do not line up.
        /// </summary>
        public static IDictionary<string, string> ArgumentsOf(TypeEntry type)
        {
            var result = new Dictionary<string, string>();

            var definition = ReferenceEquals(type, null) ? null : type.GenericDefinition;
            if (definition == null)
                return result;

            var parameters = definition.GenericArguments;
            var arguments = type.GenericArguments;

            if (parameters.Length != arguments.Length)
                return result;

            for (var idx = 0; idx < parameters.Length; idx++)
                result[parameters[idx].Name] = Of(arguments[idx]);

            return result;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The name the language gives a host type, or null when it has none.
        /// </summary>
        private static string Alias(string fullName)
        {
            return fullName != null && AliasesByFullName.TryGetValue(fullName, out var result) ? result : null;
        }

        /// <summary>
        /// Joins a definition's name with its arguments, dropping the arity reflection appends to it.
        /// </summary>
        private static string Compose(string name, IEnumerable<string> arguments)
        {
            var result = new StringBuilder(StripArity(name));

            result.Append('<');
            result.Append(string.Join(", ", arguments));
            result.Append('>');

            return result.ToString();
        }

        /// <summary>
        /// Removes the '`1' that reflection appends to the name of a generic definition.
        /// </summary>
        private static string StripArity(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var mark = name.IndexOf('`');
            return mark < 0 ? name : name.Substring(0, mark);
        }

        #endregion
    }
}
