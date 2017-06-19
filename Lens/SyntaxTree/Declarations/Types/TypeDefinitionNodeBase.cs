using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.Declarations.Types
{
    /// <summary>
    /// A base node for algebraic types and records.
    /// </summary>
    internal abstract class TypeDefinitionNodeBase<T> : NodeBase
        where T : LocationEntity
    {
        #region Constructor

        protected TypeDefinitionNodeBase()
        {
            Entries = new List<T>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name of the type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The entries of the type node.
        /// </summary>
        public List<T> Entries { get; private set; }

        #endregion

        #region Debug

        protected bool Equals(TypeDefinitionNodeBase<T> other)
        {
            return string.Equals(Name, other.Name) && Entries.SequenceEqual(other.Entries);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TypeDefinitionNodeBase<T>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Entries != null ? Entries.GetHashCode() : 0);
            }
        }

        #endregion
    }
}