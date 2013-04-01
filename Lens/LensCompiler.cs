using System;
using System.Collections.Generic;
using Lens.Parser;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree;

namespace Lens
{
	/// <summary>
	/// LENS main compiler class.
	/// https://github.com/impworks/lens
	/// </summary>
	public class LensCompiler : IDisposable
	{
		public LensCompiler(LensCompilerOptions opts = null)
		{
			m_Context = new Context(opts);
		}

		public void Dispose()
		{
			GlobalPropertyHelper.UnregisterContext(m_Context.ContextId);
		}

		private Context m_Context;

		/// <summary>
		/// Register a type to be used by LENS script.
		/// </summary>
		public void RegisterType(Type type)
		{
			RegisterType(type != null ? type.Name : null, type);
		}

		/// <summary>
		/// Registers an aliased type to be used by LENS script.
		/// </summary>
		public void RegisterType(string alias, Type type)
		{
			if (type == null)
				throw new ArgumentNullException("type");

			m_Context.ImportType(alias, type);
		}

		/// <summary>
		/// Registers a method to be used by LENS script.
		/// </summary>
		public void RegisterFunction(string name, Delegate method)
		{
			m_Context.ImportFunction(name, method);
		}

		/// <summary>
		/// Registers a dynamic property to be used by LENS script.
		/// </summary>
		public void RegisterProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			m_Context.ImportProperty(name, getter, setter);
		}

		/// <summary>
		/// Compile the script for many invocations.
		/// </summary>
		public Func<object> Compile(string src)
		{
			var nodes = new TreeBuilder().Parse(src);
			return Compile(nodes);
		}

		/// <summary>
		/// Compile the script for many invocations.
		/// </summary>
		public Func<object> Compile(IEnumerable<NodeBase> nodes)
		{
			var script = m_Context.Compile(nodes);
			return script.Run;
		}

		/// <summary>
		/// Run the script and get a return value.
		/// </summary>
		public object Run(string src)
		{
			return Compile(src)();
		}

		/// <summary>
		/// Run the script and get a return value.
		/// </summary>
		public object Run(IEnumerable<NodeBase> nodes)
		{
			return Compile(nodes)();
		}
	}
}
