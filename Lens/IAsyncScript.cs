using System.Threading.Tasks;

namespace Lens
{
    /// <summary>
    /// Interface for the compiler-generated entry point of a script that awaits at its top level.
    ///
    /// A script implements this or <see cref="IScript"/>, never both: which one it is says whether
    /// the script suspends itself, and the compiler emits the one entry point that fits. The public
    /// API hands out a delegate either way, wrapping whichever door it did not get.
    /// </summary>
    public interface IAsyncScript
    {
        /// <summary>
        /// Starts the compiled script and returns the task that completes with its value.
        ///
        /// The script runs synchronously on the calling thread until it first waits for something
        /// that has not finished, exactly as an async method does.
        /// </summary>
        Task<object> RunAsync();
    }
}
