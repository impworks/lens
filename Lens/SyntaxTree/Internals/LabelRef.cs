using System.Reflection.Emit;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// A label the lowering pass can name before there is an ILGenerator to define it in.
    ///
    /// A structured node defines its labels while emitting, because it emits them itself. A lowered
    /// body cannot: the jump and its destination are separate statements, and either of them may be
    /// emitted first. So the label is an identity first and an assembly artefact later.
    /// </summary>
    internal class LabelRef
    {
        #region Constructor

        public LabelRef(string name)
        {
            Name = name;
        }

        #endregion

        #region Fields

        /// <summary>
        /// A human-readable name, for debugging the pass itself.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The generator the label below belongs to.
        /// </summary>
        private ILGenerator _owner;

        private Label _label;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the label as the given generator knows it, defining it on first request.
        /// A body may be emitted more than once - into a fresh assembly, say - and each emission
        /// gets a label of its own.
        /// </summary>
        public Label Resolve(ILGenerator gen)
        {
            if (!ReferenceEquals(_owner, gen))
            {
                _owner = gen;
                _label = gen.DefineLabel();
            }

            return _label;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}
