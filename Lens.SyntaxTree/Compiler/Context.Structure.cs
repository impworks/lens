using System;
using System.Linq;
using System.Reflection;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Methods

		/// <summary>
		/// Add a new type to the list and ensure the name is unique.
		/// </summary>
		/// <param name="node"></param>
		public void AddType(TypeDefinitionNode node)
		{
			ensureTypeNameUniqueness(node.Name);
			DefinedTypes.Add(node);
		}

		/// <summary>
		/// Add a new record to the list and ensure the name is unique.
		/// </summary>
		/// <param name="node"></param>
		public void AddRecord(RecordDefinitionNode node)
		{
			ensureTypeNameUniqueness(node.Name);
			DefinedRecords.Add(node);
		}

		/// <summary>
		/// Resolve a type by it's string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// This means 
		/// </summary>
		/// <param name="signature"></param>
		/// <returns></returns>
		public Type ResolveType(string signature)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Generates a unique assembly name.
		/// </summary>
		private static string getAssemblyName()
		{
			lock (typeof(Context))
				_AssemblyId++;
			return "_CompiledAssembly" + _AssemblyId;
		}

		/// <summary>
		/// Ensure the name is not a defined type or a record.
		/// </summary>
		/// <param name="typeName"></param>
		private void ensureTypeNameUniqueness(string typeName)
		{
			foreach (var curr in DefinedTypes)
				if (curr.Name == typeName)
					throw new LensCompilerException(string.Format("A type named {0} is already defined!", typeName));

			foreach (var curr in DefinedRecords)
				if (curr.Name == typeName)
					throw new LensCompilerException(string.Format("A record named {0} is already defined!", typeName));
		}

		/// <summary>
		/// Registers all structures in the assembly.
		/// </summary>
		private void prepare()
		{
			foreach (var type in DefinedTypes.OfType<TypeDefinitionNodeBase>().Union(DefinedRecords))
				type.PrepareSelf(this);

			foreach (var func in DefinedFunctions)
				func.PrepareSelf(this);

			// after the types are initialized, we can now define fields
			// this solves the problem of a possible circular reference between types' fields,
			// when type A has a field of type B and type B has a field of type A
			//			foreach (var curr in DefinedTypes)
			//				prepareTypeLabels(curr);
			//
			//			foreach (var curr in DefinedRecords)
			//				prepareRecordFields(curr);
		}

		#endregion
	}
}
