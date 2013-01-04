using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Methods

		/// <summary>
		/// Resolve a type by it's string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// Therefore, you should not use GetMethod(s), GetField(s) etc. on a result of this method!
		/// </summary>
		public Type ResolveType(string type)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Resolve a field by type and field name.
		/// </summary>
		/// <returns></returns>
		public FieldInfo ResolveField(string type, string fieldName)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Resolve a constructor by type and arguments.
		/// </summary>
		public ConstructorInfo ResolveConstructor(string type, List<string> argTypes)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Resolve a method by type, name and argument types.
		/// </summary>
		public MethodInfo ResolveMethod(string type, string name, List<string> argTypes)
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
			lock (typeof (Context))
				_AssemblyId++;
			return "_CompiledAssembly" + _AssemblyId;
		}

		/// <summary>
		/// Registers all structures in the assembly.
		/// </summary>
		private void prepare()
		{
			prepareTypesAndRecords();
			prepareTypeLabelTags();
			prepareRecordFields();
		}

		/// <summary>
		/// Build assembly entities for types, labels, and records.
		/// </summary>
		private void prepareTypesAndRecords()
		{
			var reg = new Dictionary<string,bool>();
			Action<string, LocationEntity> check = (key, ent) =>
			{
				bool val;
				if (reg.TryGetValue(key, out val))
				{
					throw new LensCompilerException(
						string.Format("A {0} named '{1}' has already been defined.", val ? "type" : "type label", key),
						ent
					);
				}
			};

			foreach (var currType in _DefinedTypes)
			{
				check(currType.Name, currType);
				reg.Add(currType.Name, true);
				currType.PrepareSelf(this);

				foreach (var currLabel in currType.Entries)
				{
					check(currLabel.Name, currLabel);
					reg.Add(currLabel.Name, false);
					currLabel.PrepareSelf(currType, this);
				}
			}

			foreach (var currRec in _DefinedRecords)
			{
				check(currRec.Name, currRec);
				currRec.PrepareSelf(this);
			}
		}

		/// <summary>
		/// Prepare assembly entities for all the type label tags.
		/// </summary>
		private void prepareTypeLabelTags()
		{
			foreach(var currType in _DefinedTypes)
				foreach(var currLabel in currType.Entries)
					currLabel.PrepareTag(this);
		}

		/// <summary>
		/// Prepare assembly entities for all the record fields.
		/// </summary>
		private void prepareRecordFields()
		{
			foreach (var currRec in _DefinedRecords)
				foreach(var currField in currRec.Entries)
					currField.PrepareSelf(currRec, this);
		}

		#endregion
	}
}
