using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lens.LanguageServer
{
    /// <summary>
    /// Makes the server describe what it can do in its answer to initialize, rather than registering
    /// it afterwards.
    ///
    /// The protocol offers a server two ways to say what it supports. It can declare everything in
    /// the initialize result, or it can send client/registerCapability once the handshake is over -
    /// and which one it uses is decided by the client, by advertising dynamicRegistration. The
    /// library takes that advertisement at its word.
    ///
    /// Not every editor keeps the promise. Visual Studio advertises dynamic registration and then
    /// does nothing with the registrations, so it sees a server that supports nothing at all: no
    /// completion, no hover, no navigation, no rename, and no document edits either, since text
    /// synchronization is registered the same way. Only diagnostics get through, because those are
    /// pushed rather than asked for.
    ///
    /// Dynamic registration exists for servers whose capabilities change while they run. This one's
    /// do not - the set of things a LENS file supports is the same for every LENS file and never
    /// changes - so declaring them up front says exactly the same thing, and says it in the form
    /// every editor understands.
    /// </summary>
    internal static class StaticCapabilities
    {
        /// <summary>
        /// The property each capability carries to say it may be registered after the fact.
        /// </summary>
        private const string DynamicRegistration = "DynamicRegistration";

        /// <summary>
        /// Clears every dynamicRegistration flag the client sent, so that the library decides to
        /// declare its handlers instead of registering them.
        ///
        /// The capability tree is walked by reflection rather than property by property. The tree has
        /// upwards of forty of these flags, nested at several depths, and naming them one at a time
        /// would silently miss whichever ones a later version of the protocol adds.
        /// </summary>
        public static void Apply(object capabilities)
        {
            Visit(capabilities, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        private static void Visit(object node, HashSet<object> seen)
        {
            if (node == null || !seen.Add(node))
                return;

            var type = node.GetType();

            // only the protocol's own models are worth descending into: a string or a boxed number
            // has no capabilities under it, and the graph would otherwise wander into the framework
            if (!type.Namespace?.StartsWith("OmniSharp", StringComparison.Ordinal) == true)
                return;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                if (property.Name == DynamicRegistration && property.PropertyType == typeof(bool))
                {
                    if (property.CanWrite)
                        property.SetValue(node, false);

                    continue;
                }

                object value;

                try
                {
                    value = property.GetValue(node);
                }
                catch (TargetInvocationException)
                {
                    // a capability the client did not send throws rather than answering null
                    continue;
                }

                Visit(value, seen);
            }
        }

        /// <summary>
        /// Identity comparison for the visited set: a capability model may implement value equality,
        /// and two distinct-but-equal nodes both need visiting.
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            bool IEqualityComparer<object>.Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
