using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Represents a type to be defined in the generated assembly.
	/// </summary>
	internal class TypeEntity
	{
		public TypeEntity(Context ctx)
		{
			Context = ctx;

			_Fields = new Dictionary<string, FieldEntity>();
			_Methods = new Dictionary<string, List<MethodEntity>>();
			_Constructors = new List<ConstructorEntity>();
			_MethodList = new List<MethodEntity>();

			ClosureMethodId = 1;
		}

		#region Fields

		/// <summary>
		/// Pointer to context.
		/// </summary>
		public Context Context { get; private set; }

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
		private Dictionary<string, FieldEntity> _Fields;

		/// <summary>
		/// Temporary cache of methods.
		/// </summary>
		private List<MethodEntity> _MethodList;

		/// <summary>
		/// Method groups of the type.
		/// </summary>
		private Dictionary<string, List<MethodEntity>> _Methods;

		/// <summary>
		/// Constructors of the type.
		/// </summary>
		private List<ConstructorEntity> _Constructors;

		/// <summary>
		/// The current ID of closured methods (if the type entity is a closure backbone).
		/// </summary>
		public int ClosureMethodId { get; set; }

		/// <summary>
		/// Flag indicating the TypeBuilder for current entity has been already defined.
		/// </summary>
		protected bool _IsPrepared;

		#endregion

		#region Preparation & Compilation

		/// <summary>
		/// Generates a TypeBuilder for current type entity.
		/// </summary>
		public void PrepareSelf()
		{
			if (_IsPrepared)
				return;

			var attrs = TypeAttributes.Public;
			if(IsSealed)
				attrs |= TypeAttributes.Sealed;

			if (ParentSignature != null)
			{
				var parent = Context.FindType(ParentSignature.Signature);
				if (parent != null)
					parent.PrepareSelf();

				Parent = Context.ResolveType(ParentSignature.Signature);
				TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent);
			}
			else
			{
				TypeBuilder = Context.MainModule.DefineType(Name, attrs);
			}

			// todo: generate default ctor?

			_IsPrepared = true;
		}

		/// <summary>
		/// Invokes generation of FieldBuilder, MethodBuilder and ConstructorBuilder objects for type members.
		/// </summary>
		public void PrepareMembers()
		{
			foreach(var field in _Fields)
				field.Value.PrepareSelf();

			foreach (var ctor in _Constructors)
				ctor.PrepareSelf();

			foreach (var method in _MethodList)
				method.PrepareSelf();
		}

		/// <summary>
		/// Compile the method bodies of the current class.
		/// </summary>
		public void Compile()
		{
			foreach (var curr in _Constructors)
				curr.Compile();

			foreach (var currGroup in _Methods)
				foreach (var curr in currGroup.Value)
					curr.Compile();
				
		}

		/// <summary>
		/// Process the closured for the current type.
		/// </summary>
		public void ProcessClosures()
		{
			foreach (var curr in _MethodList)
				curr.ProcessClosures();
		}

		#endregion

		#region Structure methods

		/// <summary>
		/// Creates a new field by type signature.
		/// </summary>
		internal FieldEntity CreateField(string name, TypeSignature signature, bool isStatic = false)
		{
			var fe = createFieldCore(name, isStatic);
			fe.TypeSignature = signature;
			return fe;
		}

		/// <summary>
		/// Creates a new field by resolved type.
		/// </summary>
		internal FieldEntity CreateField(string name, Type type, bool isStatic = false)
		{
			var fe = createFieldCore(name, isStatic);
			fe.Type = type;
			return fe;
		}

		/// <summary>
		/// Creates a new method by resolved argument types.
		/// </summary>
		internal MethodEntity CreateMethod(string name, Type[] argTypes = null, bool isStatic = false, bool isVirtual = false)
		{
			var me = createMethodCore(name, isStatic, isVirtual);
			me.ArgumentTypes = argTypes;
			return me;
		}

		/// <summary>
		/// Creates a new method with argument types given by signatures.
		/// </summary>
		internal MethodEntity CreateMethod(string name, string[] argTypes = null, bool isStatic = false, bool isVirtual = false)
		{
			var args = argTypes == null
				? null
				: argTypes.Select((a, idx) => new FunctionArgument("f" + idx.ToString(), a)).ToArray();

			return CreateMethod(name, args, isStatic, isVirtual);
		}

		/// <summary>
		/// Creates a new method with argument types given by function arguments.
		/// </summary>
		internal MethodEntity CreateMethod(string name, FunctionArgument[] args = null, bool isStatic = false, bool isVirtual = false)
		{
			var argHash = new HashList<FunctionArgument>();
			if(args != null)
				foreach (var curr in args)
					argHash.Add(curr.Name, curr);

			var me = createMethodCore(name, isStatic, isVirtual);
			me.Arguments = argHash;
			return me;
		}

		/// <summary>
		/// Creates a new constructor with the given argument types.
		/// </summary>
		internal ConstructorEntity CreateConstructor(string[] argTypes = null)
		{
			var ce = new ConstructorEntity
			{
				ArgumentTypes = argTypes == null ? null : argTypes.Select(Context.ResolveType).ToArray(),
				ContainerType = this,
			};
			_Constructors.Add(ce);
			return ce;
		}

		/// <summary>
		/// Resolves a field assembly entity.
		/// </summary>
		internal FieldInfo ResolveField(string name)
		{
			FieldEntity fe;
			if (!_Fields.TryGetValue(name, out fe))
				Context.Error("Type '{0}' does not contain definition for a field '{1}'.", Name, name);

			if(fe.FieldBuilder == null)
				throw new InvalidOperationException(string.Format("Type '{0}' must be prepared before its entities can be resolved.", Name));

			return fe.FieldBuilder;
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal MethodInfo ResolveMethod(string name, Type[] args)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal ConstructorInfo ResolveConstructor(Type[] args)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Create a field without setting type info.
		/// </summary>
		private FieldEntity createFieldCore(string name, bool isStatic)
		{
			if (_Fields.ContainsKey(name))
				Context.Error("Type '{0}' already contains field '{1}'!", Name, name);

			var fe = new FieldEntity
			{
				Name = name,
				IsStatic = isStatic,
				ContainerType = this,
			};
			_Fields.Add(name, fe);
			return fe;
		}

		/// <summary>
		/// Creates a method without setting argument type info.
		/// </summary>
		private MethodEntity createMethodCore(string name, bool isStatic, bool isVirtual)
		{
			var me = new MethodEntity
			{
				Name = name,
				IsStatic = isStatic,
				IsVirtual = isVirtual,
				ContainerType = this,
			};

			_MethodList.Add(me);
			return me;
		}

		#endregion
	}
}
