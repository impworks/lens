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

		public string AssemblyName()
		{
			return string.Format(EntityNames.AssemblyNameTemplate, ++_assemblyId);
		}

		public string ClosureName()
		{
			return string.Format(EntityNames.ClosureTypeNameTemplate, ++_closureId);
		}

		public string ClosureMethodName(string methodName)
		{
			return string.Format(EntityNames.ClosureMethodNameTemplate, methodName, ++_closureMethodId);
		}

		public string ClosureFieldName(string fieldName)
		{
			return string.Format(EntityNames.ClosureFieldNameTemplate, fieldName, ++_closureFieldId);
		}

		public string AnonymousArgName()
		{
			return string.Format(EntityNames.AnonymousArgumentTemplate, ++_anonymousArgumentId);
		}

		public string TempVariableName()
		{
			return string.Format(EntityNames.ImplicitVariableNameTemplate, ++_tempVariableId);
		}
	}
}
