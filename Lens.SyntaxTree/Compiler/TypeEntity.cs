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
		#region Fields

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

		#endregion
	}
}
