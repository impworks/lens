using System.Reflection;
using System.Reflection.Emit;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.Compiler.Entities
{
    /// <summary>
    /// An assembly-level field.
    /// </summary>
    internal class FieldEntity : TypeContentsBase
    {
        #region Constructor

        public FieldEntity(TypeEntity type) : base(type)
        {
        }

        #endregion

        #region Fields

        /// <summary>
        /// Flag indicating the field belongs to the type, not its instances.
        /// </summary>
        public bool IsStatic;

        /// <summary>
        /// A string representation of the field's 
        /// </summary>
        public TypeSignature TypeSignature;

        /// <summary>
        /// Type of the values that can be saved in the field.
        /// </summary>
        public TypeEntry Type;

        /// <summary>
        /// Assembly-level field builder.
        /// </summary>
        public FieldBuilder FieldBuilder { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the type of the field.
        /// </summary>
        public override void ResolveSelf()
        {
            if (Type == null)
                Type = ContainerType.Context.ResolveType(TypeSignature);

            // a field is the one place a ref struct may not be stored, because the instance that
            // holds it may outlive the frame the ref struct is confined to. Without this it is the
            // record's generated equality members that fail, with a raw constraint-violation
            // message from EqualityComparer naming neither the field nor the script.
            //
            // Only a field the script wrote down is reported here: the ones the compiler invents
            // for a closure or a state machine are reported against the variable they hoist, which
            // is somewhere the reader can actually look.
            if (Type.IsByRefLike && Kind == TypeContentsKind.UserDefined)
                Context.Error(TypeSignature, CompilerMessages.RefStructField, Name, Type);
        }

        /// <summary>
        /// Creates a FieldBuilder for current field entity.
        /// </summary>
        public override void EmitSelf()
        {
            if (FieldBuilder != null)
                return;

            var attrs = FieldAttributes.Public;
            if (IsStatic)
                attrs |= FieldAttributes.Static;

            ResolveSelf();

            FieldBuilder = ContainerType.TypeBuilder.DefineField(Name, Type.Materialize(), attrs);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return string.Format(
                "{2} {0}.{1}",
                ContainerType.Name,
                Name,
                Type != null
                    ? Type.ToString()
                    : TypeSignature
            );
        }

        #endregion
    }
}