using System;
using System.Linq;
using System.Reflection;
using Lens.Compiler.Entities;
using Lens.Resolver;
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
		/// Imports all overrides of a method specified by name.
		/// </summary>
		/// <param name="type">Type to search in</param>
		/// <param name="name">Name of the method in type</param>
		/// <param name="newName">New name for overloaded functions</param>
		public void ImportFunctionOverloads(Type type, string name, string newName)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			importOverloads(type, name, newName, true);
		}

		/// <summary>
		/// Imports an existing external method with given name.
		/// </summary>
		public void ImportFunction(string name, MethodInfo method)
		{
			if (Options.AllowSave)
				Error(CompilerMessages.ImportIntoSaveableAssembly);

			importFunction(name, method, true);
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

		#region Helpers

		/// <summary>
		/// Imports a method from a standard library.
		/// </summary>
		private void importFunction(string name, MethodInfo method, bool check = false)
		{
			_DefinedTypes[EntityNames.MainTypeName].ImportMethod(name, method, check);
		}

		/// <summary>
		/// Imports all overrides of a method into standard library.
		/// </summary>
		private void importOverloads(Type type, string name, string newName, bool check = false)
		{
			var overloads = type.GetMethods().Where(m => m.Name == name);
			foreach (var curr in overloads)
				importFunction(newName, curr, check);
		}

		#endregion
	}
}
