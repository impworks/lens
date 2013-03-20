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
			m_RegisteredTypes = new List<Tuple<string, Type>>();
			m_RegisteredProperties = new List<Tuple<string, Delegate, Delegate>>();
			m_RegisteredFunctions = new List<Tuple<string, Delegate>>();
		}

		private List<Tuple<string, Type>> m_RegisteredTypes;
		private List<Tuple<string, Delegate>> m_RegisteredFunctions;
		private List<Tuple<string, Delegate, Delegate>> m_RegisteredProperties;

		/// <summary>
		/// Register a type to be used by LENS script.
		/// </summary>
		public void RegisterType(Type type)
		{
			RegisterType(type.Name, type);
		}

		/// <summary>
		/// Registers an aliased type to be used by LENS script.
		/// </summary>
		public void RegisterType(string alias, Type type)
		{
			m_RegisteredTypes.Add(new Tuple<string, Type>(alias, type));
		}

		/// <summary>
		/// Registers a method to be used by LENS script.
		/// </summary>
		public void RegisterFunction(string name, Delegate method)
		{
			m_RegisteredFunctions.Add(new Tuple<string, Delegate>(name, method));
		}

		/// <summary>
		/// Registers a dynamic property to be used by LENS script.
		/// </summary>
		public void RegisterProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			m_RegisteredProperties.Add(new Tuple<string, Delegate, Delegate>(name, getter, setter));
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
				ctx.ImportType(curr.Item1, curr.Item2);

			foreach(var curr in m_RegisteredFunctions)
				ctx.ImportFunction(curr.Item1, curr.Item2);

			foreach(var curr in m_RegisteredProperties)
				ctx.ImportProperty(curr.Item1, curr.Item2, curr.Item3);

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
