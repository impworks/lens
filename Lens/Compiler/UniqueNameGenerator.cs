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

		public string AnonymousArgName
		{
			get { return string.Format(EntityNames.AnonymousArgumentTemplate, ++_AnonymousArgumentId); }
		}

		public string 
	}
}
