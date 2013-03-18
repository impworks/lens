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
		/// Imports an existing external type with given name.
		/// </summary>
		public void ImportType(string name, Type type)
		{
			if (_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity(this, true)
			{
				Name = name,
				TypeInfo = type
			};
			_DefinedTypes.Add(name, te);
		}

		/// <summary>
		/// Imports an existing external method with given name.
		/// </summary>
		public void ImportMethod(string name, Delegate method)
		{
			_DefinedTypes[RootTypeName].ImportMethod(name, method);
		}

		/// <summary>
		/// Creates a new type entity with given name.
		/// </summary>
		internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor, prepare);
			te.ParentSignature = parent;
			return te;
		}

		/// <summary>
		/// Creates a new type entity with given name and a resolved type for parent.
		/// </summary>
		internal TypeEntity CreateType(string name, Type parent, bool isSealed = false, bool defaultCtor = true, bool prepare = false)
		{
			var te = createTypeCore(name, isSealed, defaultCtor, prepare);
			te.Parent = parent;
			return te;
		}

		/// <summary>
		/// Resolves a type by its string signature.
		/// Warning: this method might return a TypeBuilder as well as a Type, if the signature points to an inner type.
		/// </summary>
		public Type ResolveType(string signature)
		{
			try
			{
				TypeEntity type;
				return _DefinedTypes.TryGetValue(signature, out type)
					       ? type.TypeInfo
					       : _TypeResolver.ResolveType(signature);
			}
			catch (ArgumentException ex)
			{
				throw new LensCompilerException(ex.Message);
			}
		}

		/// <summary>
		/// Tries to search for a declared type.
		/// </summary>
		internal TypeEntity FindType(string name)
		{
			TypeEntity entity;
			_DefinedTypes.TryGetValue(name, out entity);
			return entity;
		}

		/// <summary>
		/// Tries to search for a method by it's method info.
		/// </summary>
		internal MethodEntity FindMethod(MethodInfo method)
		{
			if(!(method is MethodBuilder))
				Error("Method '{0}' is not defined within the script!", method.Name);

			var typeName = method.DeclaringType.Name;
			var type = FindType(typeName);
			if(type == null)
				Error("Type '{0}' could not be found within the script!", typeName);

			return type.FindMethod(method);
		}

		/// <summary>
		/// Resolves a method by its name and agrument list.
		/// </summary>
		public MethodInfo ResolveMethod(string typeName, string methodName, Type[] args = null)
		{
			return ResolveMethod(ResolveType(typeName), methodName, args);
		}

		/// <summary>
		/// Resolves a method by its name and agrument list.
		/// </summary>
		public MethodInfo ResolveMethod(Type type, string methodName, Type[] args = null)
		{
			if(args == null)
				args = new Type[0];

			var method = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveMethod(methodName, args)
				: ResolveMethodByArgs(type.GetMethods().Where(m => m.Name == methodName), m => m.GetParameters().Select(p => p.ParameterType).ToArray(), args).Item1;

			if(method == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any method named '{1}'.", type, methodName));

			return method;
		}

		/// <summary>
		/// Resolves a group of methods by the name.
		/// </summary>
		public MethodInfo[] ResolveMethodGroup(Type type, string methodName)
		{
			var group = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveMethodGroup(methodName)
				: type.GetMethods().Where(m => m.Name == methodName).ToArray();

			if(group == null || group.Length == 0)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any method named '{1}'.", type, methodName));

			return group;
		}

		/// <summary>
		/// Resolves a field by its name.
		/// </summary>
		public FieldInfo ResolveField(string typeName, string fieldName)
		{
			return ResolveField(ResolveType(typeName), fieldName);
		}

		/// <summary>
		/// Resolves a field by its name.
		/// </summary>
		public FieldInfo ResolveField(Type type, string fieldName)
		{
			var field = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveField(fieldName)
				: type.GetField(fieldName);

			if(field == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any field named '{1}'.", type, fieldName));

			return field;
		}

		/// <summary>
		/// Resolves a property by its name.
		/// </summary>
		public PropertyInfo ResolveProperty(Type type, string propertyName)
		{
			var pty = type is TypeBuilder
				? null
				: type.GetProperty(propertyName);

			if(pty == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain any property named '{1}'.", type, propertyName));

			return pty;
		}

		/// <summary>
		/// Resolves a constructor by it's argument list.
		/// </summary>
		public ConstructorInfo ResolveConstructor(string typeName, Type[] args = null)
		{
			return ResolveConstructor(ResolveType(typeName), args);
		}

		/// <summary>
		/// Resolves a constructor by agrument list.
		/// </summary>
		public ConstructorInfo ResolveConstructor(Type type, Type[] args = null)
		{
			if (args == null)
				args = new Type[0];

			var ctor = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveConstructor(args)
				: type.GetConstructor(args);

			if(ctor == null)
				throw new KeyNotFoundException(string.Format("Type '{0}' does not contain a constructor with the given arguments.", type));

			return ctor;
		}

		/// <summary>
		/// Resolves a type by its signature.
		/// </summary>
		public Type ResolveType(TypeSignature signature)
		{
			try
			{
				return ResolveType(signature.Signature);
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(signature);
				throw;
			}
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			var root = CreateType(node.Name);
			root.Kind = TypeEntityKind.Type;

			foreach (var curr in node.Entries)
			{
				var currType = CreateType(curr.Name, node.Name, true);
				currType.Kind = TypeEntityKind.TypeLabel;
				if (curr.IsTagged)
					currType.CreateField("Tag", curr.TagType);
			}
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			var root = CreateType(node.Name, isSealed: true);
			root.Kind = TypeEntityKind.Record;

			foreach (var curr in node.Entries)
				root.CreateField(curr.Name, curr.Type);
		}

		/// <summary>
		/// Declares a new function.
		/// </summary>
		public void DeclareFunction(FunctionNode node)
		{
			var method = MainType.CreateMethod(node.Name, node.ReturnTypeSignature, node.Arguments, true);
			method.Body = node.Body;
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
			MainMethod.Body.Add(node);
		}

		/// <summary>
		/// Resolves the best-matching method-like entity within a generic list.
		/// </summary>
		/// <typeparam name="T">Type of method-like entity.</typeparam>
		/// <param name="list">List of method-like entitites.</param>
		/// <param name="argsGetter">A function that gets method entity arguments.</param>
		/// <param name="args">Desired argument types.</param>
		public static Tuple<T, int> ResolveMethodByArgs<T>(IEnumerable<T> list, Func<T, Type[]> argsGetter, Type[] args)
		{
			Func<T, Tuple<T, int>> methodEvaluator = ent => new Tuple<T, int>(ent, ExtensionMethodResolver.GetArgumentsDistance(argsGetter(ent), args));

			var result = list.Select(methodEvaluator).OrderBy(rec => rec.Item2).ToArray();

			if (result.Length == 0 || result[0].Item2 == int.MaxValue)
				throw new KeyNotFoundException("No suitable method was found!");

			if (result.Length > 2)
			{
				var ambiCount = result.Skip(1).TakeWhile(i => i.Item2 == result[0].Item2).Count();
				if (ambiCount > 0)
					throw new AmbiguousMatchException();
			}

			return result[0];
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

			// ProcessClosures() usually processes new types, hence the caching to array
			foreach (var currType in types)
				currType.Value.ProcessClosures();
		}

		/// <summary>
		/// Prepares the assembly entities for the type list.
		/// </summary>
		private void prepareEntities()
		{
			// prepare types first
			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareSelf();

			foreach (var curr in _DefinedTypes)
				curr.Value.PrepareMembers();
		}

		/// <summary>
		/// Compiles the source code for all the declared classes.
		/// </summary>
		private void compileCore()
		{
			foreach (var curr in _DefinedTypes)
				curr.Value.Compile();
		}

		/// <summary>
		/// Create a type entry without setting its parent info.
		/// </summary>
		private TypeEntity createTypeCore(string name, bool isSealed, bool defaultCtor, bool prepare)
		{
			if (_DefinedTypes.ContainsKey(name))
				Error("Type '{0}' has already been defined!", name);

			var te = new TypeEntity(this)
			{
				Name = name,
				IsSealed = isSealed,
			};
			_DefinedTypes.Add(name, te);

			if (defaultCtor)
				te.CreateConstructor();

			if(prepare)
				te.PrepareSelf();

			return te;
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

			MainAssembly.Save("_MainModule.dll");
		}

		#endregion
	}
}