using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// The lexical score state of current method.
	/// </summary>
	internal class Scope
	{
		public Scope()
		{
			_ClosuredVariables = new HashSet<string>();
			_Variables = new Stack<Dictionary<string, LexicalNameInfo>>();
			_VariableCount = 1;
		}

		#region Fields

		/// <summary>
		/// The count of local variables used to assign IDs.
		/// </summary>
		private int _VariableCount;

		/// <summary>
		/// Currently declared variables.
		/// </summary>
		private readonly Stack<Dictionary<string, LexicalNameInfo>> _Variables;

		/// <summary>
		/// A hashlist of closured variables (maintained between compilation steps).
		/// </summary>
		private readonly HashSet<string> _ClosuredVariables;

		#endregion

		#region Methods

		/// <summary>
		/// Enters a new subscope.
		/// </summary>
		public void EnterSubscope()
		{
			_Variables.Push(new Dictionary<string, LexicalNameInfo>());
		}

		/// <summary>
		/// Leaves the current subscope, discarding any variables declared in it.
		/// </summary>
		public void LeaveSubscope()
		{
			_Variables.Pop();
		}

		/// <summary>
		/// Declares a new local variable in the scope.
		/// </summary>
		public LexicalNameInfo DeclareVariable(string name, Type type, bool isConst = false)
		{
			if (FindVariable(name) != null)
				throw new LensCompilerException(string.Format("A variable named '{0}' is already declared!", name));

			var info = new LexicalNameInfo(name, type, _VariableCount, isConst);
			_VariableCount++;
			_Variables.Peek().Add(name, info);
			return info;
		}

		/// <summary>
		/// Checks if a variable exists.
		/// </summary>
		public LexicalNameInfo FindVariable(string name)
		{
			foreach (var curr in _Variables)
			{
				LexicalNameInfo result;
				if (curr.TryGetValue(name, out result))
					return result;
			}

			return null;
		}
		
		/// <summary>
		/// Checks if the variable is closured.
		/// </summary>
		public bool IsVariableClosured(string name)
		{
			return _ClosuredVariables.Contains(name);
		}

		#endregion
	}
}
