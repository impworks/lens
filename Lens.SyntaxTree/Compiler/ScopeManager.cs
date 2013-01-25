using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.Compiler
{
	internal class ScopeManager
	{
		public ScopeManager()
		{
		}

		#region Fields

		#endregion

		#region Methods

		/// <summary>
		/// Enters a scope.
		/// </summary>
		public void EnterScope()
		{
		}

		/// <summary>
		/// Leaves a scope.
		/// </summary>
		public void LeaveScope()
		{
		}

		/// <summary>
		/// 
		/// </summary>
		public void DeclareName(string name, bool isConst, Type type)
		{
		}

		/// <summary>
		/// Checks if the variable is closured in current scope.
		/// Updates the _Closures dictionary.
		/// </summary>
		public void CheckIfClosured(string name)
		{
		}

		/// <summary>
		/// Finds a variable or a constant.
		/// </summary>
		public LocalName Find(string name)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Finds a local variable or constant inside the scopes.
		/// </summary>
		private LocalName find(string name, out bool isClosured)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Scope entry
		
		/// <summary>
		/// The lexical score state of current method.
		/// </summary>
		private class Scope
		{
			public Scope()
			{
				EntryCounter = 0;
				Entries = new Dictionary<string, LocalName>();
			}

			/// <summary>
			/// The ID for local name counters.
			/// </summary>
			public int EntryCounter;

			/// <summary>
			/// The dictionary with local names.
			/// </summary>
			public readonly Dictionary<string, LocalName> Entries;
		}

		#endregion
	}
}
