using System;
using System.Collections.Generic;
using Lens.Parser;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Compiler;

namespace Lens
{
	/// <summary>
	/// LENS main compiler class.
	/// https://github.com/impworks/lens
	/// </summary>
	public class LensCompiler
	{
		public LensCompiler()
		{
			m_RegisteredTypes = new Dictionary<string, Type>();
			m_RegisteredFunctions = new Dictionary<string, Delegate>();
		}

		private Dictionary<string, Type> m_RegisteredTypes;
		private Dictionary<string, Delegate> m_RegisteredFunctions;

		/// <summary>
		/// Register a type to be used by LENS script.
		/// </summary>
		/// <param name="type">Type to register.</param>
		public void RegisterType(Type type)
		{
			RegisterType(type.Name, type);
		}

		/// <summary>
		/// Register an aliased type to be used by LENS script.
		/// </summary>
		public void RegisterType(string alias, Type type)
		{
			if(m_RegisteredTypes.ContainsKey(alias))
				throw new LensCompilerException(string.Format("Type '{0}' has already been registered.", alias));

			m_RegisteredTypes.Add(alias, type);
		}

		/// <summary>
		/// Register a method to be used by LENS script.
		/// </summary>
		public void RegisterFunction(string name, Delegate func)
		{
			// todo: allow for overloading
			if(m_RegisteredFunctions.ContainsKey(name))
				throw new LensCompilerException(string.Format("Type '{0}' has already been registered.", name));

			m_RegisteredFunctions.Add(name, func);
		}

		/// <summary>
		/// Run the script and get a return value.
		/// </summary>
		/// <returns>The last expression of the script, evaluated.</returns>
		public object Run(string src)
		{
			var tb = new TreeBuilder();
			var nodes = tb.Parse(src);
			var script = Context.CompileNodes(nodes);
			return script.Run();
		}
	}
}
