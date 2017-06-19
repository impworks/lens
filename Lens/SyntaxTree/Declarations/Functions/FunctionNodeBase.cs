using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.SyntaxTree.Declarations.Functions
{
    /// <summary>
    /// The base node class for named functions and lambdas.
    /// </summary>
    internal abstract class FunctionNodeBase : NodeBase
    {
        #region Constructor 

        protected FunctionNodeBase()
        {
            Arguments = new List<FunctionArgument>();
        }

        #endregion

        #region Arguments

        /// <summary>
        /// Function arguments.
        /// </summary>
        public List<FunctionArgument> Arguments { get; set; }

        /// <summary>
        /// Function body.
        /// </summary>
        public CodeBlockNode Body { get; protected set; }

        #endregion

        #region Resolve

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            return Body.Resolve(ctx);
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Body, null);
        }

        #endregion

        #region Debug

        protected bool Equals(FunctionNodeBase other)
        {
            return Arguments.SequenceEqual(other.Arguments) && Equals(Body, other.Body);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FunctionNodeBase) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Arguments != null ? Arguments.GetHashCode() : 0) * 397) ^ (Body != null ? Body.GetHashCode() : 0);
            }
        }

        #endregion
    }
}