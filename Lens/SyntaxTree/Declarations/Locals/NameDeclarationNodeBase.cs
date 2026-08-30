using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Declarations.Locals
{
    /// <summary>
    /// A base class for variable and constant declarations.
    /// </summary>
    internal abstract class NameDeclarationNodeBase : NodeBase
    {
        #region Constructor

        protected NameDeclarationNodeBase(string name, bool immutable)
        {
            Name = name;
            IsImmutable = immutable;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name of the variable.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Explicitly specified local variable, already present in a scope the declaration can see.
        /// </summary>
        public Local Local { get; set; }

        /// <summary>
        /// The name this declaration is to register, rather than making one of its own.
        ///
        /// A pattern binding is resolved against the body of its 'case' long before the block that
        /// will hold the binding exists, so the uses the body records land on a name created at
        /// that point. Handing that same name over here - instead of declaring a second one beside
        /// it - is what makes 'case x then x' a single symbol an editor can rename.
        /// </summary>
        public Local Declared { get; set; }

        /// <summary>
        /// Where the name is written, when the declaration statement is not the place it is
        /// written in.
        ///
        /// A loop lowered into a state machine declares its iteration variable through a statement
        /// nobody wrote and that stands nowhere in the source, so the name it registers would be a
        /// name with no declaration to point at - and renaming it would rewrite the uses in the
        /// body and leave the header spelling the old name.
        /// </summary>
        public LocationEntity NameLocation { get; set; }

        /// <summary>
        /// Type signature for non-initialized variables.
        /// </summary>
        public TypeSignature Type { get; set; }

        /// <summary>
        /// Already resolved type for non-initialized variables, used by auto-generated code where
        /// the type has no printable signature - a generic parameter, for instance.
        /// </summary>
        public TypeEntry ResolvedType { get; set; }

        /// <summary>
        /// The value to assign to the variable.
        /// </summary>
        public NodeBase Value { get; set; }

        /// <summary>
        /// A flag indicating that the current value is read-only.
        /// </summary>
        public readonly bool IsImmutable;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            TypeEntry type;

            try
            {
                type = Value != null
                    ? Value.Resolve(ctx)
                    : (ResolvedType ?? ctx.ResolveType(Type));

                ctx.CheckTypedExpression(Value, type);
            }
            catch (LensCompilerException)
            {
                // there is no type, so there is no name either: mark it, or every use of it below
                // would report the same mistake once more
                if (Local == null)
                    ctx.Scope.DeclareFaulted(Name);

                throw;
            }

            if (Local == null)
            {
                if (Name == "_")
                    Error(CompilerMessages.UnderscoreName);

                try
                {
                    if (Declared != null)
                    {
                        ctx.Scope.DeclareLocal(Declared);
                    }
                    else
                    {
                        var name = ctx.Scope.DeclareLocal(Name, type, IsImmutable);
                        name.Declaration = NameLocation ?? this;

                        if (Value != null && Value.IsConstant && ctx.UnrollConstants)
                        {
                            name.IsConstant = true;
                            name.ConstantValue = Value.ConstantValue;
                        }
                    }
                }
                catch (LensCompilerException ex)
                {
                    ex.BindToLocation(this);
                    throw;
                }
            }

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Value, true);
        }

        internal override IReadOnlyList<NodeBase> Operands => Value == null ? NoOperands : new[] {Value};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<NameDeclarationNodeBase>();
            copy.Value = operands[0];
            return copy;
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var name = Local ?? ctx.Scope.FindLocal(Name);
            if (name.IsConstant && name.IsImmutable && ctx.UnrollConstants)
                return Expr.Unit();

            return new SetIdentifierNode
            {
                Identifier = Name,
                Local = Local,
                Value = Value ?? (ResolvedType != null ? Expr.Default(ResolvedType) : Expr.Default(Type)),
                IsInitialization = true,
            };
        }

        #endregion

        #region Debug

        protected bool Equals(NameDeclarationNodeBase other)
        {
            return IsConstant.Equals(other.IsConstant) && string.Equals(Name, other.Name) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NameDeclarationNodeBase) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IsConstant.GetHashCode();
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}({1} = {2})", IsImmutable ? "let" : "var", Name, Value);
        }

        #endregion
    }
}