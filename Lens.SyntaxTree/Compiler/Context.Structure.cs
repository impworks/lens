using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
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
			Type result;

			if (_TypeRegistry.TryGetValue(type, out result))
				return result;

			try
			{
				result = _TypeResolver.ResolveType(type);
				_TypeRegistry.Add(type, result);
				return result;
			}
			catch (Exception ex)
			{
				error(ex.Message);
			}

			return null;
		}

		/// <summary>
		/// Resolve a field by type and field name.
		/// </summary>
		/// <returns></returns>
		public FieldInfo ResolveField(string type, string fieldName)
		{
			var t = ResolveType(type);
			FieldInfo result = null;

			// type is defined internally
			if (t is TypeBuilder)
			{
				if (t.Implements<ILensRecord>())
				{
					var rec = _DefinedRecords[type].Entries.FirstOrDefault(r => r.Name == fieldName);
					if (rec != null)
						result = rec.FieldBuilder;
				}
				else if(t.Implements<ILensTypeRecord>())
				{
					if (fieldName == "Tag")
					{
						var label = _DefinedTypes.SelectMany(dt => dt.Value.Entries).FirstOrDefault(l => l.Name == type);
						if (label != null)
							result = label.FieldBuilder;
					}
				}
			}
			else
			{
				result = t.GetField(fieldName);
			}

			if (result == null)
				error("Type '{0}' does not contain a field named '{1}'.", type, fieldName);

			return result;
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

		/// <summary>
		/// Add an existing type node to the list.
		/// </summary>
		public void AddType(TypeDefinitionNode node)
		{
			var name = node.Name;
			if(_DefinedTypes.ContainsKey(name))
				error(node, "A type named '{0}' has already been defined!", name);

			_DefinedTypes.Add(name, node);
		}

		/// <summary>
		/// Add an existing type node to the list.
		/// </summary>
		public void AddRecord(RecordDefinitionNode node)
		{
			var name = node.Name;
			if (_DefinedRecords.ContainsKey(name))
				error(node, "A record named '{0}' has already been defined!", name);

			_DefinedRecords.Add(name, node);
		}

		/// <summary>
		/// Add an existing type node to the list.
		/// </summary>
		public void AddFunction(FunctionNode node)
		{
			var name = node.Name;
			List<FunctionNode> list;
			if(_DefinedFunctions.TryGetValue(name, out list))
				list.Add(node);
			else
				_DefinedFunctions.Add(name, new List<FunctionNode> { node });
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Throw a new error.
		/// </summary>
		private void error(LocationEntity entity, string err, params object[] ps)
		{
			throw new LensCompilerException(string.Format(err, ps), entity);
		}

		/// <summary>
		/// Throw a new error.
		/// </summary>
		private void error(string err, params object[] ps)
		{
			throw new LensCompilerException(string.Format(err, ps));
		}

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
			Action<string, LocationEntity> check = (key, ent) =>
			{
				Type type;
				if (_TypeRegistry.TryGetValue(key, out type))
				{
					throw new LensCompilerException(
						string.Format("A {0} named '{1}' has already been defined.", type.BaseType == typeof(object) ? "type" : "type label", key),
						ent
					);
				}
			};

			foreach (var currTypePair in _DefinedTypes)
			{
				var type = currTypePair.Value;
				var typeName = currTypePair.Key;

				check(typeName, type);
				type.PrepareSelf(this);
				_TypeRegistry.Add(typeName, type.TypeBuilder);

				foreach (var currLabel in type.Entries)
				{
					check(currLabel.Name, currLabel);
					currLabel.PrepareSelf(type, this);
					_TypeRegistry.Add(currLabel.Name, currLabel.TypeBuilder);
				}
			}

			foreach (var currRecPair in _DefinedRecords)
			{
				var rec = currRecPair.Value;
				var recName = currRecPair.Key;

				check(recName, rec);
				rec.PrepareSelf(this);
				_TypeRegistry.Add(recName, rec.TypeBuilder);
			}
		}

		/// <summary>
		/// Prepare assembly entities for all the type label tags.
		/// </summary>
		private void prepareTypeLabelTags()
		{
			foreach(var currType in _DefinedTypes)
				foreach(var currLabel in currType.Value.Entries)
					currLabel.PrepareTag(this);
		}

		/// <summary>
		/// Prepare assembly entities for all the record fields.
		/// </summary>
		private void prepareRecordFields()
		{
			foreach (var currRec in _DefinedRecords)
				foreach(var currField in currRec.Value.Entries)
					currField.PrepareSelf(currRec.Value, this);
		}

		#endregion
	}
}
