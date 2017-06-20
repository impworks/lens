namespace Lens
{
    /// <summary>
    /// Interface for the compiler-generated entry point.
    /// </summary>
    public interface IScript
    {
        /// <summary>
        /// Executes the compiled script.
        /// </summary>
        object Run();
    }
}