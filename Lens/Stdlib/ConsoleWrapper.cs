using System;

namespace Lens.Stdlib
{
	/// <summary>
	/// Standard library shorthand Console methods.
	/// </summary>
	public static class ConsoleWrapper
	{
		#region Read
		
		public static ConsoleKeyInfo ReadKey()
		{
			return Console.ReadKey();
		}

		public static ConsoleKeyInfo WaitKey()
		{
			return Console.ReadKey(true);
		}

		#endregion
	}
}
