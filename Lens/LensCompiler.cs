using System;
using Lens.Parser;
using Lens.SyntaxTree.Compiler;

namespace Lens
{
	/// <summary>
	/// LENS main compiler class.
	/// https://github.com/impworks/lens
	/// </summary>
	public class LensCompiler
	{
		/// <summary>
		/// Register a type to be used by LENS script.
		/// </summary>
		/// <param name="type">Type to register.</param>
		public void RegisterType(Type type)
		{
			RegisterType(null, type);
		}

		/// <summary>
		/// Register an aliased type to be used by LENS script.
		/// </summary>
		/// <param name="alias">Alias name.</param>
		/// <param name="type">Type to register.</param>
		public void RegisterType(string alias, Type type)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Register a method to be used by LENS script.
		/// </summary>
		/// <param name="name">Method name.</param>
		/// <param name="func">Method code.</param>
		public void RegisterFunction(string name, Delegate func)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Run the script and get a return value.
		/// </summary>
		/// <returns>The last expression of the script, evaluated.</returns>
		public object Run(string src)
		{
			var tb = new TreeBuilder();
			var nodes = tb.Parse(src);
			var ctx = Context.CreateFromNodes(nodes);
			return ctx.Execute();
		}
	}
}
