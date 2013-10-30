using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler
{
	/// <summary>
	/// Represents a type to be defined in the generated assembly.
	/// </summary>
	internal partial class TypeEntity
	{
		public TypeEntity(Context ctx)
		{
			Context = ctx;

			_Fields = new Dictionary<string, FieldEntity>();
			_Properties = new Dictionary<string, PropertyEntity>();
			_Methods = new Dictionary<string, List<MethodEntity>>();
			_Constructors = new List<ConstructorEntity>();
			_MethodList = new List<MethodEntity>();
			_Implementations = new List<Tuple<MethodInfo, MethodEntity>>();

			ClosureMethodId = 1;
		}

		public Type[] Interfaces;

		private Dictionary<string, FieldEntity> _Fields;
		private Dictionary<string, PropertyEntity> _Properties;
		private Dictionary<string, List<MethodEntity>> _Methods;
		private List<ConstructorEntity> _Constructors;

		/// <summary>
		/// Implicit interface implementations.
		/// </summary>
		private List<Tuple<MethodInfo, MethodEntity>> _Implementations;

		/// <summary>
		/// Pre-processed list of methods.
		/// </summary>
		private List<MethodEntity> _MethodList;

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
			get { return IsImported ? m_TypeInfo : TypeBuilder; }
			set
			{
				if (!IsImported)
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

		public bool IsImported
		{
			get { return Kind == TypeEntityKind.Imported; }
		}

		public bool IsUserDefined
		{
			get { return Kind == TypeEntityKind.Type || Kind == TypeEntityKind.TypeLabel || Kind == TypeEntityKind.Record; }
		}

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
			if (IsSealed)
				attrs |= TypeAttributes.Sealed;

			if (Parent != null || (ParentSignature != null && ParentSignature.FullSignature != null))
			{
				if (Parent == null)
				{
					var parentType = Context.FindType(ParentSignature.FullSignature);
					if (parentType != null)
						parentType.PrepareSelf();

					Parent = Context.ResolveType(ParentSignature.FullSignature);
				}

				TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent);
			}
			else
			{
				TypeBuilder = Context.MainModule.DefineType(Name, attrs);
			}

			if (Interfaces != null)
				foreach (var iface in Interfaces)
					TypeBuilder.AddInterfaceImplementation(iface);

			CreateEntities();
		}

		/// <summary>
		/// Invokes generation of FieldBuilder, MethodBuilder and ConstructorBuilder objects for type members.
		/// </summary>
		public void PrepareMembers()
		{
			foreach (var field in _Fields)
				field.Value.PrepareSelf();

			foreach (var field in _Properties)
				field.Value.PrepareSelf();

			foreach (var ctor in _Constructors)
				ctor.PrepareSelf();

			foreach (var method in _MethodList)
			{
				method.PrepareSelf();

				MethodEntity mi = null;
				try
				{
					mi = ResolveMethod(method.Name, method.GetArgumentTypes(Context), true);
				}
				catch (KeyNotFoundException)
				{
				}

				if (mi != null)
				{
					if (this == Context.MainType)
						Context.Error(CompilerMessages.FunctionRedefinition, method.Name);
					else
						Context.Error(CompilerMessages.MethodRedefinition, method.Name, Name);
				}

				if (!_Methods.ContainsKey(method.Name))
					_Methods.Add(method.Name, new List<MethodEntity>());

				_Methods[method.Name].Add(method);
			}

			_MethodList.Clear();
		}

		/// <summary>
		/// Compile the method bodies of the current class.
		/// </summary>
		public void Compile()
		{
			var backup = Context.CurrentType;
			Context.CurrentType = this;

			foreach (var curr in _Constructors)
				if (!curr.IsImported)
					curr.Compile();

			foreach (var currGroup in _Methods)
				foreach (var curr in currGroup.Value)
					if (!curr.IsImported)
						curr.Compile();

			Context.CurrentType = backup;
		}

		/// <summary>
		/// Creates auto-generated methods for the type.
		/// </summary>
		public void CreateEntities()
		{
			if (!IsUserDefined)
				return;

			createSpecificEquals();
			createGenericEquals();
			createGetHashCode();
		}

		/// <summary>
		/// Defines interface implementations at assembly level.
		/// </summary>
		public void DefineImplementations()
		{
			if (IsImported)
				return;

			foreach(var curr in _Implementations)
				TypeBuilder.DefineMethodOverride(curr.Item2.MethodInfo, curr.Item1);
		}

		#endregion

		#region Structure methods

		/// <summary>
		/// Imports a new method to the given type.
		/// </summary>
		internal void ImportMethod(string name, MethodInfo mi, bool check)
		{
			if (!mi.IsStatic || !mi.IsPublic)
				Context.Error(CompilerMessages.ImportUnsupportedMethod);

			var args = mi.GetParameters().Select(p => new FunctionArgument(p.Name, p.ParameterType, p.ParameterType.IsByRef));
			var me = new MethodEntity
			{
				Name = name,
				IsImported = true,
				IsStatic = true,
				IsVirtual = false,
				ContainerType = this,
				MethodInfo = mi,
				ReturnType = mi.ReturnType,
				Arguments = new HashList<FunctionArgument>(args, arg => arg.Name)
			};

			if (check)
			{
				_MethodList.Add(me);
			}
			else
			{
				if (_Methods.ContainsKey(name))
					_Methods[name].Add(me);
				else
					_Methods.Add(name, new List<MethodEntity> {me});
			}
		}

		/// <summary>
		/// Creates a new field by type signature.
		/// </summary>
		internal FieldEntity CreateField(string name, TypeSignature signature, bool isStatic = false, bool prepare = false)
		{
			var fe = createFieldCore(name, isStatic);
			fe.TypeSignature = signature;

			if (prepare)
				fe.PrepareSelf();

			return fe;
		}

		/// <summary>
		/// Creates a new field by resolved type.
		/// </summary>
		internal FieldEntity CreateField(string name, Type type, bool isStatic = false, bool prepare = false)
		{
			var fe = createFieldCore(name, isStatic);
			fe.Type = type;

			if(prepare)
				fe.PrepareSelf();

			return fe;
		}

		/// <summary>
		/// Creates a new property by resolved type.
		/// </summary>
		internal PropertyEntity CreateProperty(string name, Type type, bool hasSetter, bool isStatic = false, bool isVirtual = false, bool prepare = false)
		{
			var pe = createPropertyCore(name, hasSetter, isStatic, isVirtual);
			pe.Type = type;

			if(prepare)
				pe.PrepareSelf();

			return pe;
		}

		/// <summary>
		/// Creates a new method by resolved argument types.
		/// </summary>
		internal MethodEntity CreateMethod(string name, Type returnType, Type[] argTypes = null, bool isStatic = false, bool isVirtual = false, bool prepare = false)
		{
			var me = createMethodCore(name, isStatic, isVirtual);
			me.ArgumentTypes = argTypes;
			me.ReturnType = returnType;

			if(prepare)
				me.PrepareSelf();

			return me;
		}

		/// <summary>
		/// Creates a new method with argument types given by signatures.
		/// </summary>
		internal MethodEntity CreateMethod(string name, TypeSignature returnType, string[] argTypes = null, bool isStatic = false, bool isVirtual = false, bool prepare = false)
		{
			var args = argTypes == null
				? null
				: argTypes.Select((a, idx) => new FunctionArgument("arg" + idx.ToString(), a)).ToArray();

			return CreateMethod(name, returnType, args, isStatic, isVirtual, prepare);
		}

		/// <summary>
		/// Creates a new method with argument types given by function arguments.
		/// </summary>
		internal MethodEntity CreateMethod(string name, TypeSignature returnType, IEnumerable<FunctionArgument> args = null, bool isStatic = false, bool isVirtual = false, bool prepare = false)
		{
			var argHash = new HashList<FunctionArgument>();
			if(args != null)
				foreach (var curr in args)
					argHash.Add(curr.Name, curr);

			var me = createMethodCore(name, isStatic, isVirtual);
			me.ReturnTypeSignature = returnType;
			me.Arguments = argHash;

			if(prepare)
				me.PrepareSelf();

			return me;
		}

		/// <summary>
		/// Creates a new constructor with the given argument types.
		/// </summary>
		internal ConstructorEntity CreateConstructor(string[] argTypes = null, bool prepare = false)
		{
			var ce = new ConstructorEntity
			{
				ArgumentTypes = argTypes == null ? null : argTypes.Select(Context.ResolveType).ToArray(),
				ContainerType = this,
				Kind = MethodEntityKind.Constructor
			};

			_Constructors.Add(ce);
			Context.DeclareMethodForProcessing(ce);

			if(prepare)
				ce.PrepareSelf();

			return ce;
		}

		/// <summary>
		/// Resolves a field assembly entity.
		/// </summary>
		internal FieldEntity ResolveField(string name)
		{
			FieldEntity fe;
			if (!_Fields.TryGetValue(name, out fe))
				throw new KeyNotFoundException();

			if(fe.FieldBuilder == null)
				throw new InvalidOperationException(string.Format(CompilerMessages.TypeUnprepared, Name));

			return fe;
		}

		/// <summary>
		/// Resolves a property assembly entity.
		/// </summary>
		internal PropertyEntity ResolveProperty(string name)
		{
			PropertyEntity pe;
			if (!_Properties.TryGetValue(name, out pe))
				throw new KeyNotFoundException();

			if (pe.PropertyBuilder == null)
				throw new InvalidOperationException(string.Format(CompilerMessages.TypeUnprepared, Name));

			return pe;
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal MethodEntity ResolveMethod(string name, Type[] args, bool exact = false)
		{
			List<MethodEntity> group;
			if (!_Methods.TryGetValue(name, out group))
				throw new KeyNotFoundException();

			var info = Context.ResolveMethodByArgs(group, m => m.GetArgumentTypes(Context), args);
			if(exact && info.Item2 != 0)
				throw new KeyNotFoundException();

			return info.Item1;
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
			var info = Context.ResolveMethodByArgs(_Constructors, c => c.GetArgumentTypes(Context), args);
			return info.Item1;
		}

		/// <summary>
		/// Explicitly implements an interface.
		/// </summary>
		public void AddImplementation(MethodInfo def, MethodEntity ent)
		{
			_Implementations.Add(new Tuple<MethodInfo, MethodEntity>(def, ent));
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Create a field without setting type info.
		/// </summary>
		private FieldEntity createFieldCore(string name, bool isStatic)
		{
			if (_Fields.ContainsKey(name))
				Context.Error(CompilerMessages.TypeFieldExists, Name, name);

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
				Kind = MethodEntityKind.Function,
				ContainerType = this,
			};

			_MethodList.Add(me);
			Context.DeclareMethodForProcessing(me);

			return me;
		}

		private PropertyEntity createPropertyCore(string name, bool hasSetter, bool isStatic, bool isVirtual)
		{
			if (_Fields.ContainsKey(name))
				Context.Error(CompilerMessages.TypeFieldExists, Name, name);

			var pe = new PropertyEntity(hasSetter)
			{
				Name = name,
				IsStatic = isStatic,
				IsVirtual = isVirtual,
				ContainerType = this
			};

			_Properties.Add(name, pe);

			return pe;
		}

		#endregion
	}
}