using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Methods

		/// <summary>
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false)
		{
			if(_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity
			{
				Name = name,
				ParentSignature = parent,
				IsSealed = isSealed
			};
			te.PrepareSelf(this);
			_DefinedTypes.Add(name, te);
			return te;
		}

		/// <summary>
		/// Creates a new field in the type with the given name.
		/// </summary>
		internal FieldEntity CreateField(Type baseType, string name, Type type, bool isStatic = false)
		{
			TypeEntity typeInfo;
			if(!_DefinedTypes.TryGetValue(baseType.Name, out typeInfo))
				Error("Type '{0}' does not exist!", baseType);

			if(typeInfo.Fields.ContainsKey(name))
				Error("Type '{0}' already contains field '{1}'!", baseType, name);

			var fe = new FieldEntity
			{
				Name = name,
				Type = type,
				IsStatic = isStatic,
				ContainerType = typeInfo,
			};
			typeInfo.Fields.Add(name, fe);
			fe.PrepareSelf(this);
			return fe;
		}

		/// <summary>
		/// Creates a new method in the type with the given name.
		/// </summary>
		internal MethodEntity CreateMethod(Type baseType, string name, Type[] args = null, bool isStatic = false, bool isVirtual = false)
		{
			TypeEntity typeInfo;
			if (!_DefinedTypes.TryGetValue(baseType.Name, out typeInfo))
				Error("Type '{0}' does not exist!", baseType);

			if (typeInfo.Methods.ContainsKey(name))
			{
				var exists = (args == null || args.Length == 0)
						? typeInfo.Methods[name].Count > 0
					    : typeInfo.Methods[name].Any(m => argTypesEqual(args, m));

				if (exists)
					Error("A function named '{0}' with the same set of arguments is already defined in type '{1}'!", name, baseType);
			}
			else
			{
				typeInfo.Methods.Add(name, new List<MethodEntity>());
			}

			var me = new MethodEntity
			{
				Name = name,
				IsStatic = isStatic,
				IsVirtual = isVirtual,
				ContainerType = typeInfo,
			};
			typeInfo.Methods[name].Add(me);
			me.PrepareSelf(this);
			return me;
		}

		/// <summary>
		/// Resolves a type by it's string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			// todo: error reporting!
			TypeEntity type;
			return _DefinedTypes.TryGetValue(signature, out type)
				? type.TypeBuilder
				: _TypeResolver.ResolveType(signature);
		}

		/// <summary>
		/// Resolves a method by it's name and agrument list.
		/// </summary>
		/// <param name="type">Type to search in.</param>
		/// <param name="method">Method name.</param>
		/// <param name="args">A list of argument types.</param>
		/// <returns></returns>
		public MethodInfo ResolveMethod(string type, string method, string[] args = null)
		{
			// todo: error reporting!
			var argTypes = args.Select(ResolveType).ToArray();
			var t = ResolveType(type);
			if (t is TypeBuilder)
			{
				var meth = findMethodByArgs(_DefinedTypes[type].Methods[method], argTypes);
				return (meth as MethodEntity).MethodBuilder;
			}

			return t.GetMethod(method, argTypes);
		}

		/// <summary>
		/// Resolves a field by it's name.
		/// </summary>
		/// <param name="type">Type to search in.</param>
		/// <param name="field">Field name.</param>
		/// <returns></returns>
		public FieldInfo ResolveField(string type, string field)
		{
			// todo: error reporting!
			var t = ResolveType(type);
			return t is TypeBuilder
				? _DefinedTypes[type].Fields[field].FieldBuilder
				: t.GetField(field);
		}

		/// <summary>
		/// Resolves a constructor by it's argument list.
		/// </summary>
		/// <param name="type">Type to search in.</param>
		/// <param name="args">A list of argument types.</param>
		/// <returns></returns>
		public ConstructorInfo ResolveConstructor(string type, string[] args = null)
		{
			// todo: error reporting!
			var argTypes = args.Select(ResolveType).ToArray();
			var t = ResolveType(type);
			if (t is TypeBuilder)
			{
				var ctor = findMethodByArgs(_DefinedTypes[type].Constructors, argTypes);
				return (ctor as ConstructorEntity).ConstructorBuilder;
			}

			return t.GetConstructor(argTypes);
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			
		}

		/// <summary>
		/// Opens a new namespace for current script.
		/// </summary>
		public void DeclareOpenNamespace(UsingNode node)
		{
			_TypeResolver.AddNamespace(node.Namespace);
		}

		/// <summary>
		/// Adds a new node to the main script's body.
		/// </summary>
		public void DeclareScriptNode(NodeBase node)
		{
			_ScriptBody.Body.Add(node);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Generates a unique assembly name.
		/// </summary>
		private static string getAssemblyName()
		{
			lock (typeof(Context))
				_AssemblyId++;
			return "_CompiledAssembly" + _AssemblyId;
		}

		/// <summary>
		/// Traverses the syntactic tree, searching for closures and curried methods.
		/// </summary>
		private void processClosures()
		{
			var types = _DefinedTypes.ToArray();

			foreach (var currType in types)
				currType.Value.ProcessClosures(this);
		}

		/// <summary>
		/// Prepares the assembly entities for the type list.
		/// </summary>
		private void prepareEntities()
		{
			// prepare types first
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf(this);

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers(this);
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void compileInternal()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile(this);
		}

		/// <summary>
		/// Finalizes the assembly.
		/// </summary>
		private void finalizeAssembly()
		{
			var ep = ResolveMethod(RootTypeName, RootMethodName);
			MainAssembly.SetEntryPoint(ep, PEFileKinds.ConsoleApplication);
			foreach (var curr in _DefinedTypes)
				curr.Value.TypeBuilder.CreateType();
		}

		/// <summary>
		/// Declare the root type of the assembly.
		/// </summary>
		private void declareRootType()
		{
			var type = new TypeEntity
			{
				Name = RootTypeName,
				GenerateDefaultConstructor = true
			};

			var me = new MethodEntity
			{
				Name = RootMethodName,
				IsStatic = true,
				IsVirtual = false,
			};

			type.AddEntity(me);
		}

		/// <summary>
		/// Resolve the best-matching method of the list.
		/// </summary>
		/// <param name="methods">Source list of methods.</param>
		/// <param name="argTypes">List of argument types.</param>
		/// <returns></returns>
		private MethodEntityBase findMethodByArgs(IEnumerable<MethodEntityBase> methods, Type[] argTypes)
		{
			throw new NotImplementedException();
		}

		private bool argTypesEqual(Type[] args, MethodEntity entity)
		{
			var foundArgs = entity.Arguments.Values.Select(fa => ResolveType(fa.Type.Signature)).ToArray();
			if (args.Length != foundArgs.Length)
				return false;

			for (var idx = args.Length - 1; idx >= 0; idx--)
				if (args[idx] != foundArgs[idx])
					return false;

			return true;
		}

		#endregion
	}
}
 