using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

        #region Properties

        /// <summary>
        /// All the problems found while compiling the script.
        /// A failed compilation still throws, reporting the first error, but the full list is
        /// available here - a script with several independent mistakes yields several entries.
        /// </summary>
        public IEnumerable<Diagnostic> Diagnostics => _context.Diagnostics;

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
            return CompileSource(src, Compile);
        }

        /// <summary>
        /// Compile the script for many invocations.
        /// </summary>
        private Func<object> Compile(IEnumerable<NodeBase> nodes)
        {
            var script = _context.Compile(nodes);

            // a script that awaits at its top level cannot answer without waiting, and this is the
            // door that has to answer anyway
            if (script is IAsyncScript async)
                return () => Wait(async.RunAsync());

            return ((IScript) script).Run;
        }

        /// <summary>
        /// Compile the script for many invocations, as an operation that may suspend itself.
        ///
        /// A script that does not await runs to completion before the task is handed back, exactly
        /// as an async method containing no await does.
        /// </summary>
        public Func<Task<object>> CompileAsync(string src)
        {
            return CompileSource(src, CompileAsync);
        }

        /// <summary>
        /// Reads the source and hands the tree to whichever door was asked for.
        ///
        /// Both doors compile the same way and differ only in what they hand back: which of the two
        /// entry points the script type has is decided by the script, not by the caller.
        /// </summary>
        private T CompileSource<T>(string src, Func<IEnumerable<NodeBase>, T> compile)
        {
            try
            {
                var lexer = Measure(() => new LensLexer(src), "Lexer");
                var parser = Measure(() => new LensParser(lexer.Lexems), "Parser");
                return Measure(() => compile(parser.Nodes), "Compiler");
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
        /// Compile the script for many invocations, as an operation that may suspend itself.
        /// </summary>
        private Func<Task<object>> CompileAsync(IEnumerable<NodeBase> nodes)
        {
            var script = _context.Compile(nodes);

            if (script is IAsyncScript async)
                return async.RunAsync;

            // the script has nothing to wait for, but the caller asked for a task and must get the
            // same shape of answer either way - a failure included
            var sync = (IScript) script;
            return () => Completed(sync.Run);
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

        /// <summary>
        /// Run the script and get the task that completes with its return value.
        /// </summary>
        public Task<object> RunAsync(string src)
        {
            return CompileAsync(src)();
        }

        /// <summary>
        /// Run the script and get the task that completes with its return value.
        /// </summary>
        internal Task<object> RunAsync(IEnumerable<NodeBase> nodes)
        {
            return CompileAsync(nodes)();
        }

        #endregion

        #region Script invocation

        /// <summary>
        /// Runs a script that does not suspend itself and reports its outcome as a task.
        ///
        /// The task is never handed back incomplete: it is the shape of the answer that is uniform
        /// across scripts, not the amount of waiting involved. A failure becomes a faulted task
        /// rather than an exception out of the call, so that a caller of the asynchronous API has one
        /// error path whatever the script turned out to be.
        /// </summary>
        private static Task<object> Completed(Func<object> body)
        {
            var completion = new TaskCompletionSource<object>();

            try
            {
                completion.SetResult(body());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }

            return completion.Task;
        }

        /// <summary>
        /// Waits for a script that suspends itself, on behalf of a caller that asked for a value.
        ///
        /// Blocking on a task is safe only where the continuation that completes it can run on some
        /// other thread. Where a synchronization context has claimed the current one - a UI thread,
        /// classic ASP.NET - the continuation would be posted back to the thread doing the waiting
        /// and neither would ever proceed, so this refuses instead of hanging. A machine that never
        /// really suspended is already finished by the time we look, and needs no such care.
        /// </summary>
        private static object Wait(Task<object> task)
        {
            if (!task.IsCompleted && SynchronizationContext.Current != null)
                throw new InvalidOperationException(
                    "This script awaits at its top level, and the current thread has a synchronization context, "
                    + "so waiting for it here would deadlock. Use CompileAsync or RunAsync instead."
                );

            // not Result, and not Wait: those wrap what the script threw in an AggregateException
            return task.GetAwaiter().GetResult();
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