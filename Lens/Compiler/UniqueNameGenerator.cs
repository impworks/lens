using Lens.Compiler.Entities;

namespace Lens.Compiler
{
    /// <summary>
    /// A class that stores context-wide identifiers and returns unique names.
    /// </summary>
    internal class UniqueNameGenerator
    {
        #region Identifier fields

        private static int _assemblyId;

        private int _anonymousArgumentId;
        private int _closureId;
        private int _closureMethodId;
        private int _closureFieldId;
        private int _tempVariableId;

        #endregion

        /// <summary>
        /// Creates a new unique assembly name.
        /// </summary>
        public string AssemblyName()
        {
            return string.Format(EntityNames.AssemblyNameTemplate, ++_assemblyId);
        }

        /// <summary>
        /// Creates a new unique closure type name.
        /// </summary>
        public string ClosureName()
        {
            return string.Format(EntityNames.ClosureTypeNameTemplate, ++_closureId);
        }

        /// <summary>
        /// Creates a new unique closure method name.
        /// </summary>
        public string ClosureMethodName(string methodName)
        {
            return string.Format(EntityNames.ClosureMethodNameTemplate, methodName, ++_closureMethodId);
        }

        /// <summary>
        /// Creates a new unique closure field name.
        /// </summary>
        public string ClosureFieldName(string fieldName)
        {
            return string.Format(EntityNames.ClosureFieldNameTemplate, fieldName, ++_closureFieldId);
        }

        /// <summary>
        /// Creates a new unique anonymous argument name.
        /// </summary>
        public string AnonymousArgName()
        {
            return string.Format(EntityNames.AnonymousArgumentTemplate, ++_anonymousArgumentId);
        }

        /// <summary>
        /// Creates a new unique temporary variable name.
        /// </summary>
        public string TempVariableName()
        {
            return string.Format(EntityNames.ImplicitVariableNameTemplate, ++_tempVariableId);
        }
    }
}