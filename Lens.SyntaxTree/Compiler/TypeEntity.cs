using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Utils;
using Lens.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Represents a type to be defined in the generated assembly.
	/// </summary>
	internal class TypeEntity
	{
		public TypeEntity(Context ctx, bool isImported = false)
		{
			Context = ctx;

			_Fields = new Dictionary<string, FieldEntity>();
			_Methods = new Dictionary<string, List<MethodEntity>>();
			_Constructors = new List<ConstructorEntity>();
			_MethodList = new List<MethodEntity>();

			ClosureMethodId = 1;
			IsImported = isImported;
		}

		// todo!
		public Type[] Interfaces;

		private Dictionary<string, FieldEntity> _Fields;
		private Dictionary<string, List<MethodEntity>> _Methods;
		private List<ConstructorEntity> _Constructors;

		private List<MethodEntity> _MethodList;

		#region Properties

		/// <summary>
		/// Pointer to context.
		/// </summary>
		public Context Context { get; private set; }

		/// <summary>
		/// Checks if the type is imported from outside.
		/// </summary>
		public readonly bool IsImported;

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

			if (Parent != null || (ParentSignature != null && ParentSignature.Signature != null))
			{
				if (Parent == null)
				{
					var parentType = Context.FindType(ParentSignature.Signature);
					if (parentType != null)
						parentType.PrepareSelf();

					Parent = Context.ResolveType(ParentSignature.Signature);
				}

				TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent);
			}
			else
			{
				TypeBuilder = Context.MainModule.DefineType(Name, attrs);
			}

			if(Interfaces != null)
				foreach(var iface in Interfaces)
					TypeBuilder.AddInterfaceImplementation(iface);
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
			{
				method.PrepareSelf();

				MethodInfo mi = null;
				try
				{
					mi = ResolveMethod(method.Name, method.ArgumentTypes, true);
				}
				catch (KeyNotFoundException) { }
				
				if(mi != null)
					Context.Error("Type '{0}' already contains a method named '{1}' with identical set of arguments!", Name, method.Name);

				if(!_Methods.ContainsKey(method.Name))
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
			foreach (var currGroup in _Methods)
				foreach(var currMethod in currGroup.Value)
					currMethod.ProcessClosures();
		}

		#endregion

		#region Structure methods

		/// <summary>
		/// Imports a new method to the given type.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="method"></param>
		internal void ImportMethod(string name, Delegate method)
		{
			var info = method.GetType().GetMethod("Invoke");
			var args = info.GetParameters().Select(p => new FunctionArgument(p.Name, p.ParameterType, p.ParameterType.IsByRef ? ArgumentModifier.Ref : ArgumentModifier.In));

			var me = new MethodEntity
			{
				Name = name,
				IsStatic = info.IsStatic,
				IsVirtual = info.IsVirtual,
				ContainerType = this,
				MethodInfo = info,
				Arguments = new HashList<FunctionArgument>(args, arg => arg.Name)
			};

			_MethodList.Add(me);
		}

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
		internal MethodEntity CreateMethod(string name, IEnumerable<FunctionArgument> args = null, bool isStatic = false, bool isVirtual = false)
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
				return null;

			if(fe.FieldBuilder == null)
				throw new InvalidOperationException(string.Format("Type '{0}' must be prepared before its entities can be resolved.", Name));

			return fe.FieldBuilder;
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal MethodInfo ResolveMethod(string name, Type[] args, bool exact = false)
		{
			List<MethodEntity> group;
			if (!_Methods.TryGetValue(name, out group))
				return null;

			var info = resolveMethodByArgs(group, args);
			if(exact && info.Item2 != 0)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain a method named '{1}' with given exact arguments!", Name, name));

			return (info.Item1 as MethodEntity).MethodBuilder;
		}

		/// <summary>
		/// Resolves a group of methods by their name.
		/// </summary>
		internal MethodInfo[] ResolveMethodGroup(string name)
		{
			List<MethodEntity> group;
			return _Methods.TryGetValue(name, out group)
				? group.Select(m => m.MethodBuilder).ToArray()
				: new MethodInfo[0];
		}

		/// <summary>
		/// Resolves a method assembly entity.
		/// </summary>
		internal ConstructorInfo ResolveConstructor(Type[] args)
		{
			var info = resolveMethodByArgs(_Constructors, args);
			return (info.Item1 as ConstructorEntity).ConstructorBuilder;
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

		/// <summary>
		/// Resolve a method within a list by closest distance to the given parameters.
		/// </summary>
		private Tuple<MethodEntityBase, int> resolveMethodByArgs(IEnumerable<MethodEntityBase> list, Type[] args)
		{
			Func<MethodEntityBase, Tuple<MethodEntityBase, int>> methodEvaluator = ent =>
			{
				ent.PrepareSelf();
				return new Tuple<MethodEntityBase, int>(ent, ExtensionMethodResolver.GetArgumentsDistance(ent.ArgumentTypes, args));
			};

			var result = list.Select(methodEvaluator).OrderBy(rec => rec.Item2).ToArray();
			
			if(result.Length == 0 || result[0].Item2 == int.MaxValue)
				throw new KeyNotFoundException("No suitable method was found!");

			if (result.Length > 2)
			{
				var ambiCount = result.Skip(1).TakeWhile(i => i.Item2 == result[0].Item2).Count();
				if(ambiCount > 0)
					throw new AmbiguousMatchException();
			}

			return result[0];
		}

		#endregion
	}

	/// <summary>
	/// A kind of type entity defined in the type manager.
	/// </summary>
	internal enum TypeEntityKind
	{
		Internal,
		Type,
		TypeLabel,
		Record
	}
}