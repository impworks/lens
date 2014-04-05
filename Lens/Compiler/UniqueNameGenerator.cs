using Lens.Compiler.Entities;

namespace Lens.Compiler
{
	/// <summary>
	/// A class that stores context-wide identifiers and returns unique names.
	/// </summary>
	internal class UniqueNameGenerator
	{
		#region Identifier fields

		private int _AssemblyId;
		private int _AnonymousArgumentId;
		private int _ClosureId;
		private int _ClosureMethodId;
		private int _ClosureFieldId;
		private int _TempVariableId;

		#endregion

		public string AssemblyName()
		{
			return string.Format(EntityNames.AssemblyNameTemplate, ++_AssemblyId);
		}

		public string ClosureName()
		{
			return string.Format(EntityNames.ClosureTypeNameTemplate, ++_ClosureId);
		}

		public string ClosureMethodName(string methodName)
		{
			return string.Format(EntityNames.ClosureMethodNameTemplate, methodName, ++_ClosureMethodId);
		}

		public string ClosureFieldName(string fieldName)
		{
			return string.Format(EntityNames.ClosureFieldNameTemplate, fieldName, ++_ClosureFieldId);
		}

		public string AnonymousArgName()
		{
			return string.Format(EntityNames.AnonymousArgumentTemplate, ++_AnonymousArgumentId);
		}

		public string TempVariableName()
		{
			return string.Format(EntityNames.ImplicitVariableNameTemplate, ++_TempVariableId);
		}
	}
}
