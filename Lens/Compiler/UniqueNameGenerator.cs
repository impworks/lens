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

		public string AssemblyName
		{
			get { return string.Format(EntityNames.AssemblyNameTemplate, ++_AssemblyId); }
		}

		public string ClosureName
		{
			get { return string.Format(EntityNames.ClosureTypeNameTemplate, ++_ClosureId); }
		}

		public string ClosureMethodName
		{
			get { return string.Format(EntityNames.ClosureMethodNameTemplate, ++_ClosureMethodId); }
		}

		public string ClosureFieldName
		{
			get { return string.Format(EntityNames.ClosureFieldNameTemplate, ++_ClosureFieldId); }
		}

		public string AnonymousArgName
		{
			get { return string.Format(EntityNames.AnonymousArgumentTemplate, ++_AnonymousArgumentId); }
		}

		public string TempVariableName
		{
			get { return string.Format(EntityNames.ImplicitVariableNameTemplate, ++_TempVariableId); }
		}
	}
}
