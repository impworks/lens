using System;
using System.Reflection;
using Lens.Compiler.Entities;
using Lens.Translations;

namespace Lens.Compiler
{
	internal partial class Context
	{
		/// <summary>
		/// Imports an existing external type with given name.
		/// </summary>
		public void ImportType(string name, Type type)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			if (_DefinedTypes.ContainsKey(name))
				Error(CompilerMessages.TypeDefined, name);

			var te = new TypeEntity(this)
			{
				Name = name,
				Kind = TypeEntityKind.Imported,
				TypeInfo = type
			};
			_DefinedTypes.Add(name, te);
		}

		/// <summary>
		/// Imports a method from a standard library.
		/// </summary>
		public void ImportFunctionUnchecked(string name, MethodInfo method, bool check = false)
		{
			_DefinedTypes[EntityNames.MainTypeName].ImportMethod(name, method, check);
		}

		/// <summary>
		/// Imports an existing external method with given name.
		/// </summary>
		public void ImportFunction(string name, MethodInfo method)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			ImportFunctionUnchecked(name, method, true);
		}

		/// <summary>
		/// Imports a property registered in GlobalPropertyHelper into the lookup.
		/// </summary>
		public void ImportProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			if (_DefinedProperties.ContainsKey(name))
				Error(CompilerMessages.PropertyImported, name);

			var ent = GlobalPropertyHelper.RegisterProperty(ContextId, getter, setter);
			_DefinedProperties.Add(name, ent);
		}
	}
}
