using System;

namespace Lens.Stdlib
{
    /// <summary>
    /// Standard library shorthand Console methods.
    /// </summary>
    public static class ConsoleWrapper
    {
        #region Read
        
        // ReSharper disable once UnusedMember.Global
        public static ConsoleKeyInfo ReadKey()
        {
            return Console.ReadKey();
        }

        // ReSharper disable once UnusedMember.Global
        public static ConsoleKeyInfo WaitKey()
        {
            return Console.ReadKey(true);
        }

        #endregion
    }
}