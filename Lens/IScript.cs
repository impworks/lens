namespace Lens
{
    /// <summary>
    /// Interface for the compiler-generated entry point.
    ///
    /// A script that awaits at its top level implements <see cref="IAsyncScript"/> instead: it
    /// cannot produce its value without suspending, and this interface has nowhere to say so.
    /// </summary>
    public interface IScript
    {
        /// <summary>
        /// Executes the compiled script.
        /// </summary>
        object Run();
    }
}