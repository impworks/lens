using Lens.Compiler;

namespace Lens.SyntaxTree.Declarations.Types
{
    /// <summary>
    /// A node representing the record definition construct.
    /// 
    /// This node is for parser only.
    /// </summary>
    internal class RecordDefinitionNode : TypeDefinitionNodeBase<RecordField>
    {
    }

    /// <summary>
    /// Definition of a record entry.
    /// </summary>
    internal class RecordField : LocationEntity
    {
        #region Fields

        /// <summary>
        /// The name of the entry.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the entry.
        /// </summary>
        public TypeSignature Type { get; set; }

        #endregion

        #region Debug

        protected bool Equals(RecordField other)
        {
            return string.Equals(Name, other.Name) && Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RecordField) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
            }
        }

        #endregion
    }
}