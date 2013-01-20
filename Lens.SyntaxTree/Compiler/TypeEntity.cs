using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Represents a type to be defined in the generated assembly.
	/// </summary>
	internal class TypeEntity
	{
		public TypeEntity()
		{
			Fields = new Dictionary<string, FieldEntity>();
			Methods = new Dictionary<string, List<MethodEntity>>();
			Constructors = new List<ConstructorEntity>();
		}

		#region Fields

		public bool GenerateDefaultConstructor { get; set; }

		public string Name { get; set; }

		public Type Parent { get; set; }

		public TypeBuilder TypeBuilder { get; private set; }

		public Dictionary<string, FieldEntity> Fields { get; set; }

		public Dictionary<string, List<MethodEntity>> Methods { get; set; }

		public List<ConstructorEntity> Constructors { get; private set; }

		#endregion

		#region

		public void PrepareSelf(Context ctx)
		{
			TypeBuilder = ctx.MainModule.DefineType(Name, TypeAttributes.Public, Parent);
		}

		public void PrepareMembers(Context ctx)
		{
			foreach(var field in Fields)
				field.Value.PrepareSelf(ctx);

			foreach (var methodGroup in Methods)
				foreach(var method in methodGroup.Value)
					method.PrepareSelf(ctx);

			foreach(var ctor in Constructors)
				ctor.PrepareSelf(ctx);
		}

		/// <summary>
		/// Adds a constructor entity to the list.
		/// </summary>
		public void AddEntity(ConstructorEntity ctor)
		{
			Constructors.Add(ctor);
			ctor.ContainerType = this;
		}

		/// <summary>
		/// Adds a method entity to the list.
		/// </summary>
		public void AddEntity(MethodEntity method)
		{
			if(Methods.ContainsKey(method.Name))
				Methods[method.Name].Add(method);
			else
				Methods.Add(method.Name, new List<MethodEntity> { method });

			method.ContainerType = this;
		}

		/// <summary>
		/// Adds a field entity to the list.
		/// </summary>
		/// <param name="field"></param>
		public void AddEntity(FieldEntity field)
		{
			Fields.Add(field.Name, field);
			field.ContainerType = this;
		}

		#endregion
	}
}
