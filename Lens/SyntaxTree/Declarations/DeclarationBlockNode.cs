using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;

namespace Lens.SyntaxTree.Declarations
{
    /// <summary>
    /// A block that spells out the environment the host is expected to provide: its assemblies,
    /// its global variables, its functions and its type aliases.
    ///
    /// The block has two readers and two meanings. The compiler already has a host, so it treats
    /// the block as an assertion and checks every entry against what was actually registered. A
    /// language server has no host at all, so it treats the block as the definition of the
    /// environment - which is the only way an editor can know what a script is allowed to call.
    ///
    /// This node is for parser only: it emits no code.
    /// </summary>
    internal class DeclarationBlockNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The entries of the block, in source order.
        /// </summary>
        public List<DeclarationEntryBase> Entries { get; } = new List<DeclarationEntryBase>();

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            // does nothing
            // all DeclarationBlockNodes are processed by Context.LoadTree()
        }

        #endregion

        #region Debug

        protected bool Equals(DeclarationBlockNode other)
        {
            return Entries.SequenceEqual(other.Entries);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DeclarationBlockNode) obj);
        }

        public override int GetHashCode()
        {
            return Entries.Count;
        }

        public override string ToString()
        {
            return $"declare({string.Join(", ", Entries)})";
        }

        #endregion
    }

    /// <summary>
    /// A base for a single entry of a declaration block.
    /// </summary>
    internal abstract class DeclarationEntryBase : LocationEntity
    {
    }

    /// <summary>
    /// An assembly the script expects to have been referenced.
    ///
    /// The compiler ignores this entirely - the embedding host has already decided which assemblies
    /// exist, via RegisterAssembly - so a path that does not resolve is not a compilation problem.
    /// It is here for tooling, which has nothing else to go on.
    /// </summary>
    internal class DeclaredReference : DeclarationEntryBase
    {
        #region Fields

        /// <summary>
        /// Absolute path to the assembly, or a path relative to the script's own location.
        /// </summary>
        public string Path { get; set; }

        #endregion

        #region Debug

        protected bool Equals(DeclaredReference other)
        {
            return string.Equals(Path, other.Path);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DeclaredReference) obj);
        }

        public override int GetHashCode()
        {
            return Path != null ? Path.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return $"reference({Path})";
        }

        #endregion
    }

    /// <summary>
    /// A global variable the host is expected to have registered with RegisterProperty.
    /// </summary>
    internal class DeclaredProperty : DeclarationEntryBase
    {
        #region Fields

        /// <summary>
        /// The name by which the script refers to the property.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type the property is expected to have. Matched exactly, not by assignability: a
        /// property declared wider than it is would make an editor offer the wrong members.
        /// </summary>
        public TypeSignature Type { get; set; }

        /// <summary>
        /// Whether the script expects to be able to assign to the property ("var" rather than "let").
        /// </summary>
        public bool IsMutable { get; set; }

        #endregion

        #region Debug

        protected bool Equals(DeclaredProperty other)
        {
            return string.Equals(Name, other.Name) && Equals(Type, other.Type) && IsMutable == other.IsMutable;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DeclaredProperty) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name != null ? Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsMutable.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{(IsMutable ? "var" : "let")}({Name}:{Type})";
        }

        #endregion
    }

    /// <summary>
    /// A function the host is expected to have registered with RegisterFunction or
    /// RegisterFunctionOverloads.
    ///
    /// One name may be declared several times, as long as the argument signatures differ: that is
    /// what an overload group registered by RegisterFunctionOverloads looks like from here.
    /// </summary>
    internal class DeclaredFunction : DeclarationEntryBase
    {
        #region Fields

        /// <summary>
        /// The name by which the script calls the function.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The return type. A missing signature means the function returns nothing, exactly as it
        /// does for a function declared in the script.
        /// </summary>
        public TypeSignature ReturnTypeSignature { get; set; }

        /// <summary>
        /// The arguments, whose types are mandatory here: there is no body to infer them from.
        /// </summary>
        public List<FunctionArgument> Arguments { get; set; } = new List<FunctionArgument>();

        #endregion

        #region Debug

        protected bool Equals(DeclaredFunction other)
        {
            return string.Equals(Name, other.Name)
                   && Equals(ReturnTypeSignature, other.ReturnTypeSignature)
                   && Arguments.SequenceEqual(other.Arguments);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DeclaredFunction) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Name != null ? Name.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (ReturnTypeSignature != null ? ReturnTypeSignature.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Arguments.Count;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"fun({Name}:{ReturnTypeSignature}, {string.Join(" ", Arguments.Select(x => x.Name))})";
        }

        #endregion
    }

    /// <summary>
    /// A short local name for a host type.
    ///
    /// Unlike the other entries this one is a definition rather than an assertion: given the
    /// referenced assemblies the compiler can resolve the type itself, so the alias works whether
    /// or not the host also registered it with RegisterType. When the host did register it, the
    /// two must agree.
    /// </summary>
    internal class DeclaredTypeAlias : DeclarationEntryBase
    {
        #region Fields

        /// <summary>
        /// The name the script uses.
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// The type it stands for.
        /// </summary>
        public TypeSignature Type { get; set; }

        #endregion

        #region Debug

        protected bool Equals(DeclaredTypeAlias other)
        {
            return string.Equals(Alias, other.Alias) && Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DeclaredTypeAlias) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Alias != null ? Alias.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return $"type({Alias} = {Type})";
        }

        #endregion
    }
}
