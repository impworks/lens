using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// Base node for collections: dictionaries, arrays, lists, etc.
    /// </summary>
    internal abstract class CollectionNodeBase<T> : NodeBase
    {
        #region Constructor

        protected CollectionNodeBase()
        {
            Expressions = new List<T>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The list of items.
        /// </summary>
        public List<T> Expressions { get; set; }

        #endregion

        #region Resolve

        protected TypeEntry ResolveItemType(IEnumerable<NodeBase> nodes, Context ctx)
        {
            try
            {
                // a lambda literal among the items has to settle before the items can be compared:
                // the collection stores it as the delegate it becomes, and nothing here names one
                var types = nodes.Select(n => ctx.SettleLambda(n)).ToArray();
                return types.GetMostCommonType(ctx.Resolver);
            }
            catch (LensCompilerException ex)
            {
                // the items are resolved here too, and whatever goes wrong inside one of them is
                // already bound to the item that has it. Only a failure that belongs to no single
                // item - no common type across them - is the collection's own to report.
                if (ex.StartLocation == null || ex.EndLocation == null)
                    ex.BindToLocation(this);

                throw;
            }
        }

        #endregion

        #region Debug

        protected bool Equals(CollectionNodeBase<T> other)
        {
            return Expressions.SequenceEqual(other.Expressions);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CollectionNodeBase<T>) obj);
        }

        public override int GetHashCode()
        {
            return (Expressions != null ? Expressions.GetHashCode() : 0);
        }

        #endregion
    }
}