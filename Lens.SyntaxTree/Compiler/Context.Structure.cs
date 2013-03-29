using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;
using Lens.Utils;

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
			if(Options.AllowSave)
				Error("Entities cannot be imported into a saveable assembly!");

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
		public void ImportFunction(string name, Delegate method)
		{
			if (Options.AllowSave)
				Error("Entities cannot be imported into a saveable assembly!");

			_DefinedTypes[RootTypeName].ImportMethod(name, method);
		}

		/// <summary>
		/// Imports a property registered in GlobalPropertyHelper into the lookup.
		/// </summary>
		public void ImportProperty<T>(string name, Func<T> getter, Action<T> setter = null)
		{
			if (Options.AllowSave)
				Error("Entities cannot be imported into a saveable assembly!");

			if(_DefinedProperties.ContainsKey(name))
				Error("Property '{0}' has already been imported!", name);

			var ent = GlobalPropertyHelper.RegisterProperty(ContextId, getter, setter);
			_DefinedProperties.Add(name, ent);
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
			if(!_DefinedTypes.TryGetValue(name, out entity))
				throw new KeyNotFoundException();

			return entity;
		}

		/// <summary>
		/// Tries to search for a method by its info.
		/// </summary>
		internal MethodEntity FindMethod(MethodInfo method)
		{
			if(!(method is MethodBuilder))
				Error("Method '{0}' is not defined within the script!", method.Name);

			var typeName = method.DeclaringType.Name;
			var type = FindType(typeName);
			if(type == null)
				throw new KeyNotFoundException();

			return type.FindMethod(method);
		}

		/// <summary>
		/// Tries to search for a constructor by its info.
		/// </summary>
		internal ConstructorEntity FindConstructor(ConstructorInfo ctor)
		{
			if (!(ctor is ConstructorBuilder))
				Error("Type '{0}' is not defined within the script!", ctor.DeclaringType.Name);

			var typeName = ctor.DeclaringType.Name;
			var type = FindType(typeName);
			if (type == null)
				throw new KeyNotFoundException();

			return type.FindConstructor(ctor);
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

			MethodInfo method;
			if (type is TypeBuilder)
			{
				method = _DefinedTypes[type.Name].ResolveMethod(methodName, args);
			}
			else
			{
				IEnumerable<MethodInfo> methods;
				try
				{
					methods = type.GetMethods().Where(m => m.Name == methodName);
				}
				catch (NotSupportedException)
				{
					// type is a generic type that references a dynamic type generated with LENS
					// going the long & hard way...
					var genType = type.GetGenericTypeDefinition();
					methods = genType.GetMethods()
					                 .Where(m => m.Name == methodName)
					                 .Select(m => TypeBuilder.GetMethod(type, m));
				}

				method = ResolveMethodByArgs(methods, m => m.GetParameters().Select(p => p.ParameterType).ToArray(), args).Item1;
			}

			if(method == null)
				throw new KeyNotFoundException();

			return method;
		}

		/// <summary>
		/// Resolves a group of methods by the name.
		/// </summary>
		public IEnumerable<MethodInfo> ResolveMethodGroup(Type type, string methodName)
		{
			var group = type is TypeBuilder
				? _DefinedTypes[type.Name].ResolveMethodGroup(methodName)
				: type.GetMethods().Where(m => m.Name == methodName);

			if(group == null || !group.Any())
				throw new KeyNotFoundException();

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
			FieldInfo field;
			if (type is TypeBuilder)
			{
				field = _DefinedTypes[type.Name].ResolveField(fieldName);
			}
			else
			{
				try
				{
					field = type.GetField(fieldName);
				}
				catch (NotSupportedException)
				{
					var genType = type.GetGenericTypeDefinition();
					var origField = genType.GetField(fieldName);
					field = origField == null ? null : TypeBuilder.GetField(type, origField);
				}
			}

			if(field == null)
				throw new KeyNotFoundException();

			return field;
		}

		/// <summary>
		/// Resolves a property by its name.
		/// </summary>
		public MethodInfo ResolvePropertyGetter(Type type, string propertyName)
		{
			return resolvePropertyAccessor(type, propertyName, p => p.GetGetMethod());
		}

		/// <summary>
		/// Resolves a property by its name.
		/// </summary>
		public MethodInfo ResolvePropertySetter(Type type, string propertyName)
		{
			return resolvePropertyAccessor(type, propertyName, p => p.GetSetMethod());
		}

		private MethodInfo resolvePropertyAccessor(Type type, string propertyName, Func<PropertyInfo, MethodInfo> acc)
		{
			// built-in types have no properties fo'chizzle.
			if (type is TypeBuilder)
				throw new KeyNotFoundException();

			try
			{
				var pty = type.GetProperty(propertyName);
				if (pty == null)
					throw new KeyNotFoundException();

				var accValue = acc(pty);
				if(accValue == null)
					throw new ArgumentNullException();

				return accValue;
			}
			catch (NotSupportedException)
			{
				var genType = type.GetGenericTypeDefinition();
				var origPty = genType.GetProperty(propertyName);
				if (origPty == null)
					throw new KeyNotFoundException();

				var accValue = acc(origPty);
				if(accValue == null)
					throw new ArgumentNullException();

				return TypeBuilder.GetMethod(type, accValue);
			}
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

			ConstructorInfo ctor;
			if (type is TypeBuilder)
			{
				ctor = _DefinedTypes[type.Name].ResolveConstructor(args);
			}
			else
			{
				try
				{
					ctor = type.GetConstructor(args);
				}
				catch (NotSupportedException)
				{
					var genType = type.GetGenericTypeDefinition();
					var ctors = genType.GetConstructors().Select(c => TypeBuilder.GetConstructor(type, c));
					ctor = ResolveMethodByArgs(ctors, c => c.GetParameters().Select(p => p.ParameterType).ToArray(), args).Item1;
				}
			}

			if(ctor == null)
				throw new KeyNotFoundException();

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

		internal GlobalPropertyInfo ResolveGlobalProperty(string name)
		{
			GlobalPropertyInfo ent;
			if(!_DefinedProperties.TryGetValue(name, out ent))
				throw new KeyNotFoundException();

			return ent;
		}

		/// <summary>
		/// Declares a new type.
		/// </summary>
		public void DeclareType(TypeDefinitionNode node)
		{
			var mainType = CreateType(node.Name, prepare: true);
			mainType.Kind = TypeEntityKind.Type;

			foreach (var curr in node.Entries)
			{
				var tagName = curr.Name;
				var labelType = CreateType(tagName, node.Name, isSealed: true, prepare: true);
				labelType.Kind = TypeEntityKind.TypeLabel;

				var ctor = labelType.CreateConstructor();
				if (curr.IsTagged)
				{
					labelType.CreateField("Tag", curr.TagType);

					var args = new HashList<FunctionArgument> { { "value", new FunctionArgument("value", curr.TagType) } };

					var staticCtor = RootType.CreateMethod(tagName, tagName, new string[0], true);
					ctor.Arguments = staticCtor.Arguments = args;

					ctor.Body.Add(
						Expr.SetMember(Expr.This(), "Tag", Expr.Get("value"))
					);

					staticCtor.Body.Add(
						Expr.New(tagName, Expr.Get("value"))
					);
				}
				else
				{
					var staticCtor = labelType.CreateMethod(tagName, tagName, new string[0], true);
					staticCtor.Body.Add(Expr.New(tagName));

					var pty = GlobalPropertyHelper.RegisterProperty(ContextId, labelType.TypeInfo, staticCtor, null);
					_DefinedProperties.Add(tagName, pty);
				}
			}
		}

		/// <summary>
		/// Declares a new record.
		/// </summary>
		public void DeclareRecord(RecordDefinitionNode node)
		{
			var recType = CreateType(node.Name, isSealed: true);
			recType.Kind = TypeEntityKind.Record;

			var recCtor = recType.CreateConstructor();

			foreach (var curr in node.Entries)
			{
				var field = recType.CreateField(curr.Name, curr.Type);
				var argName = "_" + field.Name.ToLowerInvariant();

				recCtor.Arguments.Add(argName, new FunctionArgument(argName, curr.Type));
				recCtor.Body.Add(
					Expr.SetMember(Expr.This(), field.Name, Expr.Get(argName))
				);
			}
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
			Func<T, Tuple<T, int>> methodEvaluator = ent => new Tuple<T, int>(ent, ExtensionMethodResolver.GetArgumentsDistance(args, argsGetter(ent)));

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

		/// <summary>
		/// Checks if the expression returns a value and has a specified type.
		/// </summary>
		public void CheckTypedExpression(NodeBase node, Type calculatedType = null, bool allowNull = false)
		{
			var type = calculatedType ?? node.GetExpressionType(this);

			if(!allowNull && type == typeof(NullType))
				Error(node, "Expression type cannot be inferred! Please use type casting to specify actual type.");

			if(type.IsVoid())
				Error(node, "Expression that returns a value is expected!");
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
		/// Initializes the context from a stream of nodes.
		/// </summary>
		private void loadNodes(IEnumerable<NodeBase> nodes)
		{
			foreach (var currNode in nodes)
			{
				if (currNode is TypeDefinitionNode)
					DeclareType(currNode as TypeDefinitionNode);
				else if (currNode is RecordDefinitionNode)
					DeclareRecord(currNode as RecordDefinitionNode);
				else if (currNode is FunctionNode)
					DeclareFunction(currNode as FunctionNode);
				else if (currNode is UsingNode)
					DeclareOpenNamespace(currNode as UsingNode);
				else
					DeclareScriptNode(currNode);
			}
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
//			var ep = ResolveMethod(RootTypeName, RootMethodName);
//			MainAssembly.SetEntryPoint(ep, PEFileKinds.ConsoleApplication);

			foreach (var curr in _DefinedTypes)
				curr.Value.TypeBuilder.CreateType();

			if(Options.AllowSave)
				MainAssembly.Save("_MainModule.dll");
		}

		#endregion
	}
}