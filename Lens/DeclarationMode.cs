namespace Lens
{
    /// <summary>
    /// What a 'declare' block means to a compilation.
    ///
    /// The block describes the environment the host provides, and it has two readers. A compiler
    /// already has a host, so the block is an assertion about it. A language server has no host at
    /// all, so the block is the only description of the environment there is - and treating it as
    /// one is what lets an editor offer the API a script exists to call.
    /// </summary>
    public enum DeclarationMode
    {
        /// <summary>
        /// Check every declaration against what the host registered, and report the differences.
        /// This is what an embedding host wants.
        /// </summary>
        Verify,

        /// <summary>
        /// Register whatever the host has not, so that the declared environment exists. Nothing is
        /// callable at runtime this way - a declared function has no body and a declared variable no
        /// value - so this is for analysis only, and a compilation that emits IL must not use it.
        /// </summary>
        Provide
    }
}
