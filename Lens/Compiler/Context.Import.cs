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
        /// Registers a new assembly in resolvers.
        /// </summary>
        public void RegisterAssembly(Assembly asm)
        {
            AssemblyCache.ReferenceAssembly(asm);
        }

        /// <summary>
        /// Imports an existing external type with given name.
        /// </summary>
        public void ImportType(string name, Type type)
        {
            if (Options.AllowSave)
                Error(CompilerMessages.ImportIntoSaveableAssembly);

            if (_definedTypes.ContainsKey(name))
                Error(CompilerMessages.TypeDefined, name);

            var te = new TypeEntity(this)
            {
                Name = name,
                Kind = TypeEntityKind.Imported,
                TypeInfo = type
            };
            _definedTypes.Add(name, te);
        }

        /// <summary>
        /// Imports all overrides of a method specified by name.
        /// </summary>
        /// <param name="type">Type to search in.</param>
        /// <param name="name">Name of the method in type.</param>
        /// <param name="newName">New name for overloaded functions.</param>
        public void ImportFunctionOverloads(Type type, string name, string newName = null)
        {
            if (Options.AllowSave)
                Error(CompilerMessages.ImportIntoSaveableAssembly);

            ImportOverloads(type, name, newName ?? name, true);
        }

        /// <summary>
        /// Imports an existing external method with given name.
        /// </summary>
        public void ImportFunction(string name, MethodInfo method)
        {
            if (Options.AllowSave)
                Error(CompilerMessages.ImportIntoSaveableAssembly);

            ImportFunction(name, method, true);
        }

        /// <summary>
        /// Imports a delegate as a function.
        /// </summary>
        public void ImportFunction<T>(string name, T @delegate)
            where T: class
        {
            if(!(@delegate is Delegate))
                throw new ArgumentException(nameof(@delegate));

            ImportProperty(name, () => @delegate);
        }

        /// <summary>
        /// Imports a property registered in GlobalPropertyHelper into the lookup.
        /// </summary>
        public void ImportProperty<T>(string name, Func<T> getter, Action<T> setter = null)
        {
            if (Options.AllowSave)
                Error(CompilerMessages.ImportIntoSaveableAssembly);

            if (_definedProperties.ContainsKey(name))
                Error(CompilerMessages.PropertyImported, name);

            var ent = GlobalPropertyHelper.RegisterProperty(ContextId, getter, setter);
            _definedProperties.Add(name, ent);
        }

        #region Helpers

        /// <summary>
        /// Imports a method from a standard library.
        /// </summary>
        private void ImportFunction(string name, MethodInfo method, bool check)
        {
            _definedTypes[EntityNames.MainTypeName].ImportMethod(name, method, check);
        }

        /// <summary>
        /// Imports all overrides of a method into standard library.
        /// </summary>
        private void ImportOverloads(Type type, string name, string newName, bool check = false)
        {
            var overloads = type.GetMethods(BindingFlags.Static | BindingFlags.Public)
                                .Where(m => m.Name == name)
                                .ToList();

            if (overloads.Count == 0)
                Error(CompilerMessages.NoOverloads, name, type.Name);

            foreach (var curr in overloads)
                ImportFunction(newName, curr, check);
        }

        #endregion
    }
}