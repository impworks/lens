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
			ClosureMethodId = 1;
		}

		#region Fields

		/// <summary>
		/// Checks if the type cannot be inherited from.
		/// </summary>
		public bool IsSealed { get; set; }

		/// <summary>
		/// Checks if the compiler should generate a parameterless constructor for the type.
		/// </summary>
		public bool GenerateDefaultConstructor { get; set; }

		/// <summary>
		/// Type name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// A signature for parent type that might be declared later.
		/// </summary>
		public TypeSignature ParentSignature { get; set; }

		/// <summary>
		/// The resolved parent type.
		/// </summary>
		public Type Parent { get; private set; }

		/// <summary>
		/// The typebuilder for current type.
		/// </summary>
		public TypeBuilder TypeBuilder { get; private set; }

		/// <summary>
		/// Fields of the type.
		/// </summary>
		public Dictionary<string, FieldEntity> Fields { get; set; }

		/// <summary>
		/// Method groups of the type.
		/// </summary>
		public Dictionary<string, List<MethodEntity>> Methods { get; set; }

		/// <summary>
		/// Constructors of the type.
		/// </summary>
		public List<ConstructorEntity> Constructors { get; private set; }

		/// <summary>
		/// The current ID of closured methods (if the type entity is a closure backbone).
		/// </summary>
		public int ClosureMethodId { get; set; }

		/// <summary>
		/// Flag indicating the TypeBuilder for current entity has been already defined.
		/// </summary>
		protected bool _IsPrepared;

		/// <summary>
		/// Flag indicating the type-dependent members of this type has been created.
		/// </summary>
		protected bool _MembersGenerated;

		#endregion

		#region Methods

		/// <summary>
		/// Generates a TypeBuilder for current type entity.
		/// </summary>
		public void PrepareSelf(Context ctx)
		{
			if (_IsPrepared)
				return;

			var attrs = TypeAttributes.Public;
			if(IsSealed)
				attrs |= TypeAttributes.Sealed;

			if (ParentSignature != null)
			{
				Parent = ctx.ResolveType(ParentSignature.Signature);
				TypeBuilder = ctx.MainModule.DefineType(Name, attrs, Parent);
			}
			else
			{
				TypeBuilder = ctx.MainModule.DefineType(Name, attrs);
			}

			// todo: generate default ctor?

			_IsPrepared = true;
		}

		/// <summary>
		/// Protects dynamic member generation from double execution.
		/// </summary>
		public void GenerateMembers(Context ctx)
		{
			if(!_MembersGenerated)
				generateMembers(ctx);

			_MembersGenerated = true;
		}

		/// <summary>
		/// Invokes generation of FieldBuilder, MethodBuilder and ConstructorBuilder objects for type members.
		/// </summary>
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
		public void AddEntity(FieldEntity field)
		{
			Fields.Add(field.Name, field);
			field.ContainerType = this;
		}

		/// <summary>
		/// Compile the method bodies of the current class.
		/// </summary>
		public void Compile(Context ctx)
		{
			foreach (var curr in Constructors)
				curr.Compile(ctx);

			foreach (var currGroup in Methods)
				foreach (var curr in currGroup.Value)
					curr.Compile(ctx);
				
		}

		/// <summary>
		/// Process the closured for the current type.
		/// </summary>
		public void ProcessClosures(Context ctx)
		{
			foreach (var currGroup in Methods.Values)
				foreach (var currMethod in currGroup)
					currMethod.ProcessClosures(ctx);
		}

		/// <summary>
		/// Generate the dynamic members of this type.
		/// </summary>
		protected virtual void generateMembers(Context ctx)
		{
			
		}

		#endregion
	}
}
