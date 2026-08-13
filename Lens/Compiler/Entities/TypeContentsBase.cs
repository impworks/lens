namespace Lens.Compiler.Entities
{
    /// <summary>
    /// The base class of a type-contained entity.
    /// </summary>
    internal abstract class TypeContentsBase
    {
        #region Constructor

        protected TypeContentsBase(TypeEntity type)
        {
            ContainerType = type;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name of the current entity.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type that contains current entity.
        /// </summary>
        public readonly TypeEntity ContainerType;

        /// <summary>
        /// The kind of the current entity.
        /// </summary>
        public TypeContentsKind Kind;

        /// <summary>
        /// The analysis half of preparation: resolves everything the declaration itself states -
        /// the types of the signature - and creates nothing the assembly would hold.
        /// </summary>
        public abstract void ResolveSelf();

        /// <summary>
        /// The emission half of preparation: creates the builder for the entity and everything
        /// that hangs off it. Requires the container type to have a TypeBuilder.
        /// </summary>
        public abstract void EmitSelf();

        /// <summary>
        /// Creates the assembly instances for the current entity, resolving its signature first
        /// if that has not happened yet.
        /// </summary>
        public void PrepareSelf()
        {
            ResolveSelf();
            EmitSelf();
        }

        /// <summary>
        /// Prepares the entity as far as the current compilation goes: the signature always, the
        /// builders only when there is somewhere to emit them into.
        /// </summary>
        public void PrepareSelfAsNeeded()
        {
            ResolveSelf();

            if (ContainerType.Context.IsEmitting)
                EmitSelf();
        }

        #endregion
    }
}