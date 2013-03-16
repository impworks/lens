using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Parser;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

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
			m_RegisteredFunctions = new Dictionary<string, List<Delegate>>();
		}

		private Dictionary<string, Type> m_RegisteredTypes;
		private Dictionary<string, List<Delegate>> m_RegisteredFunctions;

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
				throw new LensCompilerException(string.Format("Type '{0}' has already been registered!", alias));

			m_RegisteredTypes.Add(alias, type);
		}

		/// <summary>
		/// Register a method to be used by LENS script.
		/// </summary>
		public void RegisterFunction(string name, Delegate method)
		{
			if (m_RegisteredFunctions.ContainsKey(name))
			{
				var types = method.GetType().GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
				var overloads = m_RegisteredFunctions[name];
				if(overloads.Any(d => d.GetArgumentTypes().SequenceEqual(types)))
					throw new LensCompilerException(string.Format("Method '{0}' with identical argument types has already been registered!", name));

				overloads.Add(method);
			}
			else
			{
				m_RegisteredFunctions.Add(name, new List<Delegate> {method});
			}
		}

		/// <summary>
		/// Compile the script for many invocations.
		/// </summary>
		public Func<object> Compile(string src)
		{
			var tb = new TreeBuilder();
			var nodes = tb.Parse(src);
			var ctx = Context.CreateFromNodes(nodes);

			foreach (var curr in m_RegisteredTypes)
				ctx.ImportType(curr.Key, curr.Value);

			var script = ctx.Compile();
			
			return script.Run;
		}

		/// <summary>
		/// Run the script and get a return value.
		/// </summary>
		public object Run(string src)
		{
			return Compile(src)();
		}
	}
}
