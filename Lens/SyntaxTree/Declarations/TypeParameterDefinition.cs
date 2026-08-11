using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;

namespace Lens.SyntaxTree.Declarations
{
    /// <summary>
    /// A generic parameter as written at a declaration site, together with its inline constraints:
    ///
    ///     fun sum&lt;T = IComparable&lt;T&gt;&gt; (items:T[]) -&gt; ...
    ///
    /// This entity is for the parser only - it becomes a GenericParameterEntity during compilation.
    /// </summary>
    internal class TypeParameterDefinition : LocationEntity
    {
        #region Constructor

        public TypeParameterDefinition()
        {
            TypeConstraints = new List<TypeSignature>();
            Keywords = new List<string>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name of the parameter.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The parameter is constrained to reference types ("class").
        /// </summary>
        public bool IsReferenceType { get; set; }

        /// <summary>
        /// The parameter is constrained to non-nullable value types ("struct").
        /// </summary>
        public bool IsValueType { get; set; }

        /// <summary>
        /// The parameter is constrained to types with a public parameterless constructor ("new").
        /// </summary>
        public bool RequiresDefaultCtor { get; set; }

        /// <summary>
        /// The signatures of type constraints, in declaration order.
        /// Whether a signature denotes a base type or an interface is only known once it is resolved.
        /// </summary>
        public List<TypeSignature> TypeConstraints { get; }

        /// <summary>
        /// The keyword constraints in declaration order, kept so that duplicates can be reported.
        /// </summary>
        public List<string> Keywords { get; }

        #endregion

        #region Debug

        protected bool Equals(TypeParameterDefinition other)
        {
            return string.Equals(Name, other.Name)
                   && IsReferenceType == other.IsReferenceType
                   && IsValueType == other.IsValueType
                   && RequiresDefaultCtor == other.RequiresDefaultCtor
                   && TypeConstraints.SequenceEqual(other.TypeConstraints);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((TypeParameterDefinition) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name != null ? Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ IsReferenceType.GetHashCode();
                hashCode = (hashCode * 397) ^ IsValueType.GetHashCode();
                hashCode = (hashCode * 397) ^ RequiresDefaultCtor.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}
