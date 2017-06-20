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

        protected Type ResolveItemType(IEnumerable<NodeBase> nodes, Context ctx)
        {
            try
            {
                var types = nodes.Select(n => n.Resolve(ctx)).ToArray();
                return types.GetMostCommonType();
            }
            catch (LensCompilerException ex)
            {
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