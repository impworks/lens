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

		public bool IsSealed { get; set; }

		public bool GenerateDefaultConstructor { get; set; }

		public string Name { get; set; }

		public TypeSignature ParentSignature { get; set; }

		public Type Parent { get; private set; }

		public TypeBuilder TypeBuilder { get; private set; }

		public Dictionary<string, FieldEntity> Fields { get; set; }

		public Dictionary<string, List<MethodEntity>> Methods { get; set; }

		public List<ConstructorEntity> Constructors { get; private set; }

		public int ClosureMethodId { get; set; }

		protected bool _IsPrepared;

		#endregion

		#region Methods

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
			compileMethodList(ctx, Constructors);

			foreach(var curr in Methods)
				compileMethodList(ctx, curr.Value);
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
		/// Compiles a list of methods.
		/// </summary>
		private void compileMethodList(Context ctx, IEnumerable<MethodEntityBase> methods)
		{
			foreach (var curr in methods)
			{
				ctx.CurrentMethod = curr;
				// curr.Compile();
				ctx.CurrentMethod = null;
			}
		}

		#endregion
	}
}
