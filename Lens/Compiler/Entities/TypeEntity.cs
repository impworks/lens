using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.Compiler.Entities
{
	/// <summary>
	/// Represents a type to be defined in the generated assembly.
	/// </summary>
	internal partial class TypeEntity : IPreparableEntity
	{
		public TypeEntity(Context ctx)
		{
			Context = ctx;

			_Fields = new Dictionary<string, FieldEntity>();
			_Methods = new Dictionary<string, List<MethodEntity>>();
			_Constructors = new List<ConstructorEntity>();

			ClosureMethodId = 1;
		}

		public Type[] Interfaces;

		private readonly Dictionary<string, FieldEntity> _Fields;
		private readonly Dictionary<string, List<MethodEntity>> _Methods;
		private readonly List<ConstructorEntity> _Constructors;

		#region Properties

		/// <summary>
		/// Pointer to context.
		/// </summary>
		public Context Context { get; private set; }

		/// <summary>
		/// Checks if the type cannot be inherited from.
		/// </summary>
		public bool IsSealed;

		/// <summary>
		/// Type name.
		/// </summary>
		public string Name;

		/// <summary>
		/// A signature for parent type that might be declared later.
		/// </summary>
		public TypeSignature ParentSignature;

		/// <summary>
		/// The resolved parent type.
		/// </summary>
		public Type Parent;

		private Type m_TypeInfo;
		public Type TypeInfo
		{
			get { return TypeBuilder ?? m_TypeInfo; }
			set
			{
				if(!IsImported)
					throw new LensCompilerException(string.Format("Type '{0}' is not imported!", Name));

				m_TypeInfo = value;
			}
		}

		/// <summary>
		/// The typebuilder for current type.
		/// </summary>
		public TypeBuilder TypeBuilder { get; private set; }

		/// <summary>
		/// The current ID of closured methods (if the type entity is a closure backbone).
		/// </summary>
		public int ClosureMethodId;

		/// <summary>
		/// A kind of LENS type this entity represents.
		/// </summary>
		public TypeEntityKind Kind;

		public bool IsImported { get { return Kind == TypeEntityKind.Imported; } }

		public bool IsUserDefined { get { return Kind == TypeEntityKind.Type || Kind == TypeEntityKind.TypeLabel || Kind == TypeEntityKind.Record; } }

		#endregion

		#region Preparation & Compilation

		/// <summary>
		/// Generates a TypeBuilder for current type entity.
		/// </summary>
		public void PrepareSelf()
		{
			if (TypeBuilder != null || IsImported)
				return;

			var attrs = TypeAttributes.Public;
			if(IsSealed)
				attrs |= TypeAttributes.Sealed;

			if (Parent == null && ParentSignature != null)
				Parent = Context.ResolveType(ParentSignature);

			TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent);

			if(Interfaces != null)
				foreach(var iface in Interfaces)
					TypeBuilder.AddInterfaceImplementation(iface);
		}

		/// <summary>
		/// Compile the method bodies of the current class.
		/// </summary>
		public void Compile()
		{
			Context.CurrentType = this;

			foreach (var curr in _Constructors)
				if (!curr.IsImported)
					curr.Compile();

			foreach (var currGroup in _Methods)
				foreach (var curr in currGroup.Value)
					if(!curr.IsImported)
						curr.Compile();
		}

		/// <summary>
		/// Creates auto-generated methods for the type.
		/// </summary>
		public void CreateEntities()
		{
			if (IsUserDefined)
			{
				createSpecificEquals();
				createGenericEquals();
				createGetHashCode();	
			}

			if (this == Context.MainType)
			{
				foreach (var currGroup in _Methods)
					foreach (var currMethod in currGroup.Value)
						if (currMethod.IsPure)
							createPureWrapper(currMethod);		
			}
		}

		#endregion

		#region Structure methods

		/// <summary>
		/// Resolves a field assembly entity.
		/// </summary>
		internal FieldEntity ResolveField(string name)
		{
			FieldEntity fe;
			if (!_Fields.TryGetValue(name, out fe))
				throw new KeyNotFoundException();

			if(fe.FieldBuilder == null)
				throw new InvalidOperationException(string.Format("Type '{0}' must be prepared before its entities can be resolved.", Name));

			return fe;
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal MethodEntity ResolveMethod(string name, Type[] args, bool exact = false)
		{
			List<MethodEntity> group;
			if (!_Methods.TryGetValue(name, out group))
				throw new KeyNotFoundException();

			var info = Context.ResolveMethodByArgs(
				group,
				m => m.GetArgumentTypes(Context),
				m => m.IsVariadic,
				args
			);

			if(exact && info.Distance != 0)
				throw new KeyNotFoundException();

			return info.Method;
		}

		/// <summary>
		/// Resolves a group of methods by their name.
		/// </summary>
		internal MethodEntity[] ResolveMethodGroup(string name)
		{
			List<MethodEntity> group;
			if(!_Methods.TryGetValue(name, out group))
				throw new KeyNotFoundException();
			
			return group.ToArray();
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal ConstructorEntity ResolveConstructor(Type[] args)
		{
			var info = Context.ResolveMethodByArgs(_Constructors, c => c.GetArgumentTypes(Context), c => false, args);
			return info.Method;
		}

		#endregion
	}
}