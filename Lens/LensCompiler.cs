using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Lens.Compiler;
using Lens.Lexer;
using Lens.Parser;
using Lens.Resolver;
using Lens.SyntaxTree;

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
            _context = new Context(opts);
            Measurements = new Dictionary<string, TimeSpan>();
        }

        #region Fields

        /// <summary>
        /// Timings of various compiler states (for debug purposes).
        /// </summary>
        public readonly Dictionary<string, TimeSpan> Measurements;

        /// <summary>
        /// The main context class.
        /// </summary>
        private readonly Context _context;

        #endregion

        #region Methods

        /// <summary>
        /// Register an assembly to be used by the LENS script.
        /// </summary>
        public void RegisterAssembly(Assembly asm)
        {
            _context.RegisterAssembly(asm);
        }

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
                throw new ArgumentNullException(nameof(type));

            _context.ImportType(alias, type);
        }

        /// <summary>
        /// Registers a method to be used by LENS script.
        /// </summary>
        public void RegisterFunction(string name, MethodInfo method)
        {
            _context.ImportFunction(name, method);
        }

        /// <summary>
        /// Registers a delegate as a method to be used by LENS script.
        /// </summary>
        public void RegisterFunction<T>(string name, T @delegate)
            where T: Delegate
        {
            _context.ImportFunction(name, @delegate);
        }

        /// <summary>
        /// Registers a list of overloaded methods to be used by LENS script.
        /// </summary>
        /// <param name="type">Source type.</param>
        /// <param name="name">The name of the group of source methods.</param>
        /// <param name="newName">The new name of the methods that will be available in the LENS script. Equals the source name by default.</param>
        public void RegisterFunctionOverloads(Type type, string name, string newName = null)
        {
            _context.ImportFunctionOverloads(type, name, newName);
        }

        /// <summary>
        /// Registers a dynamic property to be used by LENS script.
        /// </summary>
        public void RegisterProperty<T>(string name, Func<T> getter, Action<T> setter = null)
        {
            _context.ImportProperty(name, getter, setter);
        }

        /// <summary>
        /// Compile the script for many invocations.
        /// </summary>
        public Func<object> Compile(string src)
        {
            try
            {
                var lexer = Measure(() => new LensLexer(src), "Lexer");
                var parser = Measure(() => new LensParser(lexer.Lexems), "Parser");
                var λ = Measure(() => Compile(parser.Nodes), "Compiler");
                return λ;
            }
            catch (LensCompilerException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new LensCompilerException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Compile the script for many invocations.
        /// </summary>
        private Func<object> Compile(IEnumerable<NodeBase> nodes)
        {
            var script = _context.Compile(nodes);
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
        internal object Run(IEnumerable<NodeBase> nodes)
        {
            return Compile(nodes)();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Prints out debug information about compilation stage timing if Options.DebugOutput flag is set.
        /// </summary>
        [DebuggerStepThrough]
        private T Measure<T>(Func<T> action, string title)
        {
            var start = DateTime.Now;
            var res = action();
            var end = DateTime.Now;

            if (_context.Options.MeasureTime)
                Measurements[title] = end - start;

            return res;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            GlobalPropertyHelper.UnregisterContext(_context.ContextId);
        }

        #endregion
    }
}