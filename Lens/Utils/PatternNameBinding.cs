using System;

namespace Lens.Utils
{
    /// <summary>
    /// A name bound in the pattern matching rule together with its type.
    /// </summary>
    internal class PatternNameBinding
    {
        #region Constructor

        public PatternNameBinding(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name as a string.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The type corresponding to this name.
        /// </summary>
        public readonly Type Type;

        #endregion

        #region Debug

        protected bool Equals(PatternNameBinding other)
        {
            return string.Equals(Name, other.Name) && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PatternNameBinding) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name.GetHashCode() * 397) ^ Type.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Name}:{Type}";
        }

        #endregion
    }
}