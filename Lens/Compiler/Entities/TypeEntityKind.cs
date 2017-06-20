namespace Lens.Compiler.Entities
{
    /// <summary>
    /// A kind of type entity defined in the type manager.
    /// </summary>
    internal enum TypeEntityKind
    {
        /// <summary>
        /// Algebraic type
        /// </summary>
        Type,

        /// <summary>
        /// Label of an algebraic type (child of base type)
        /// </summary>
        TypeLabel,

        /// <summary>
        /// Record with several fields
        /// </summary>
        Record,

        /// <summary>
        /// Hidden type for lambda functions and arguments captured by them
        /// </summary>
        Closure,

        /// <summary>
        /// Types imported by compiler configuration
        /// </summary>
        Imported,

        /// <summary>
        /// The type that contains assembly's entry point
        /// </summary>
        Main
    }
}