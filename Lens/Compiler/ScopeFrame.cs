using System.Collections.Generic;

namespace Lens.Compiler
{
	/// <summary>
	/// A list of variables defined in a particular code block.
	/// </summary>
	internal class ScopeFrame
	{
		public ScopeFrame()
		{
			Names = new Dictionary<string, LocalName>();
		}

		/// <summary>
		/// The lookup table of names defined in current scope.
		/// </summary>
		public Dictionary<string, LocalName> Names;
	}
}
