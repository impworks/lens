using System;

namespace Lens.Playground
{
    /// <summary>
    /// Turns the browser runtime's own refusals into sentences a script author can act on.
    ///
    /// The compiler emits IL, and the runtime under a browser tab is Mono's interpreter, which
    /// supports a little less of Reflection.Emit than the desktop runtime does. Where it refuses,
    /// it refuses with a resource key rather than a sentence, and the compiler passes that through
    /// as the message of the exception it wraps. Left alone, the pane would show
    /// "PlatformNotSupported_UserDefinedSubclassesOfType" over a line of perfectly reasonable
    /// LENS, which tells the author nothing about what to write instead.
    /// </summary>
    internal static class PlatformLimits
    {
        /// <summary>
        /// The refusal that arrives when a local's type is an array of something that is not yet a
        /// finished CLR type: a record or algebraic type the script declared, or a generic
        /// parameter.
        ///
        /// Declaring a local needs a real type, and neither of those is one at the moment the local
        /// is declared - the array of either is a synthetic type object that this runtime declines
        /// to accept. Every other shape works, lists included, which is what makes the advice below
        /// worth giving.
        /// </summary>
        private const string ArrayOfGeneratedType =
            "Arrays of a type that is still being compiled are not supported in the browser: that "
            + "means arrays of records and algebraic types declared in the script, and arrays of a "
            + "generic parameter such as T[]. The .NET runtime a browser tab runs cannot build one. "
            + "Use a list instead: write 'new [[a; b]]' rather than 'new [a; b]', 'ToList ()' rather "
            + "than 'ToArray ()', and 'items:List<T>' rather than 'items:T[]'. Lists, dictionaries, "
            + "tuples and sequences of such types all work, and so do arrays of ordinary types.";

        /// <summary>
        /// The message to show for a failure, which is the original one unless this platform is
        /// known to be the reason for it.
        /// </summary>
        public static string Explain(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is NotSupportedException && IsGeneratedTypeRefusal(current.Message))
                    return ArrayOfGeneratedType;
            }

            return ex.Message;
        }

        /// <summary>
        /// Both spellings the runtime uses for this refusal: the resource key when the message
        /// resources have been stripped, and the sentence when they have not.
        /// </summary>
        private static bool IsGeneratedTypeRefusal(string message)
        {
            if (message == null)
                return false;

            return message.IndexOf("UserDefinedSubclassesOfType", StringComparison.Ordinal) >= 0
                   || message.IndexOf("subclasses of System.Type", StringComparison.Ordinal) >= 0;
        }
    }
}
