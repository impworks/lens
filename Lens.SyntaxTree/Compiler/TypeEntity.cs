using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.Translations;
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

				MethodEntity mi = null;
				try
				{
					mi = ResolveMethod(method.Name, method.GetArgumentTypes(Context), true);
				}
				catch (KeyNotFoundException) { }

				if (mi != null)
				{
					if(this == Context.MainType)
						Context.Error(CompilerMessages.FunctionRedefinition, method.Name);
					else
						Context.Error(CompilerMessages.MethodRedefinition, method.Name, Name);
				}

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
			var backup = Context.CurrentType;
			Context.CurrentType = this;

			foreach (var curr in _Constructors)
				if (!curr.IsImported)
					curr.Compile();

			foreach (var currGroup in _Methods)
				foreach (var curr in currGroup.Value)
					if(!curr.IsImported)
						curr.Compile();

			Context.CurrentType = backup;
		}

		/// <summary>
		/// Process the closured for the current type.
		/// </summary>
		public void ProcessClosures()
		{
			foreach (var currGroup in _Methods)
				foreach(var currMethod in currGroup.Value)
					if (!currMethod.IsImported)
						currMethod.ProcessClosures();

			foreach(var currCtor in _Constructors)
				if (!currCtor.IsImported)
					currCtor.ProcessClosures();
		}

		/// <summary>
		/// Creates auto-generated methods for the type.
		/// </summary>
		public void CreateEntities()
		{
			if (Kind != TypeEntityKind.Internal)
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

		#region Auto-generated entities

		private void createSpecificEquals()
		{
			var eq = CreateMethod("Equals", "bool", new[] { Expr.Arg("other", Name) });

			// var result = true
			eq.Body.Add(Expr.Var("result", Expr.True()));

			foreach (var f in _Fields.Values)
			{
				var left = Expr.GetMember(Expr.This(), f.Name);
				var right = Expr.GetMember(Expr.Get("other"), f.Name);

				var isSeq = f.Type.IsGenericType && f.Type.Implements(typeof (IEnumerable<>), true);
				var expr = isSeq
					? Expr.Invoke("Enumerable", "SequenceEqual", left, right)
					: Expr.Invoke(Expr.This(), "Equals",  Expr.Cast(left, "object"), Expr.Cast(right, "object"));

				eq.Body.Add(
					Expr.Set(
						"result",
						Expr.And(Expr.Get("result"), expr)
					)
				);
			}
			
			eq.Body.Add(Expr.Get("result"));
		}

		private void createGenericEquals()
		{
			var eq = CreateMethod(
				"Equals",
				"bool",
				new[] { Expr.Arg("obj", "object") },
				false,
				true
			);

			// if(this.ReferenceEquals null obj)
			//    false
			// else
			//    (this.ReferenceEquals this obj) || ( (obj.GetType () == this.GetType()) && (this.Equals obj as <Name>))

			eq.Body.Add(
				Expr.If(
					Expr.Invoke(Expr.This(), "ReferenceEquals", Expr.Null(), Expr.Get("obj")),
					Expr.Block(Expr.False()),
					Expr.Block(
						Expr.Or(
							Expr.Invoke(Expr.This(), "ReferenceEquals", Expr.This(), Expr.Get("obj")),
							Expr.And(
								Expr.Equal(
									Expr.Invoke(Expr.Get("obj"), "GetType"),
									Expr.Invoke(Expr.This(), "GetType")
								),
								Expr.Invoke(
									Expr.This(),
									"Equals",
									Expr.Cast(Expr.Get("obj"), Name)
								)
							)
						)
					)
				)
			);
		}

		private void createGetHashCode()
		{
			var ghc = CreateMethod(
				"GetHashCode",
				typeof (int),
				Type.EmptyTypes,
				false,
				true
			);

			// var result = 0
			ghc.Body.Add(Expr.Var("result", Expr.Int(0)));

			// result ^= (<field> != null ? field.GetHashCode() : 0) * 397
			var id = 0;
			foreach (var f in _Fields.Values)
			{
				var fieldType = f.Type ?? Context.ResolveType(f.TypeSignature);
				NodeBase expr;
				if (fieldType.IsIntegerType())
					expr = Expr.GetMember(Expr.This(), f.Name);
				else if (fieldType.IsValueType)
					expr = Expr.Invoke(
						Expr.Cast(Expr.GetMember(Expr.This(), f.Name), typeof(object)),
						"GetHashCode"
					);
				else
					expr = Expr.If(
						Expr.NotEqual(
							Expr.GetMember(Expr.This(), f.Name),
							Expr.Null()
						),
						Expr.Block(
							Expr.Invoke(
								Expr.GetMember(Expr.This(), f.Name),
								"GetHashCode"
							)
						),
						Expr.Block(Expr.Int(0))
					);

				if (id < _Fields.Count - 1)
					expr = Expr.Mult(expr, Expr.Int(397));

				ghc.Body.Add(
					Expr.Set("result", Expr.Xor(Expr.Get("result"), expr))
				);

				id++;
			}

			ghc.Body.Add(Expr.Get("result"));
		}

		private void createPureWrapper(MethodEntity method)
		{
			if(method.ReturnType.IsVoid())
				Context.Error(CompilerMessages.PureFunctionReturnUnit, method.Name);

			var pureName = string.Format(EntityNames.PureMethodNameTemplate, method.Name);
			var pure = CreateMethod(pureName, method.ReturnTypeSignature, method.Arguments.Values, true);
			pure.Body = method.Body;
			method.Body = null;

			var argCount = method.Arguments != null ? method.Arguments.Count : method.ArgumentTypes.Length;

			if (argCount >= 8)
				Context.Error(CompilerMessages.PureFunctionTooManyArgs, method.Name);

			if(argCount == 0)
				createPureWrapper0(method, pureName);
			else if(argCount == 1)
				createPureWrapper1(method, pureName);
			else
				createPureWrapperMany(method, pureName);
		}

		private void createPureWrapper0(MethodEntity wrapper, string originalName)
		{
			var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
			var flagName = string.Format(EntityNames.PureMethodCacheFlagNameTemplate, wrapper.Name);
			
			CreateField(fieldName, wrapper.ReturnTypeSignature, true);
			CreateField(flagName, typeof(bool), true);

			wrapper.Body = Expr.Block(
				
				// if (not $flag) $cache = $internal (); $flag = true
				Expr.If(
					Expr.Not(Expr.GetMember(EntityNames.MainTypeName, flagName)),
					Expr.Block(
						Expr.SetMember(
							EntityNames.MainTypeName,
							fieldName,
							Expr.Invoke(EntityNames.MainTypeName, originalName)
						),
						Expr.SetMember(EntityNames.MainTypeName, flagName, Expr.True())
					)
				),

				// $cache
				Expr.GetMember(EntityNames.MainTypeName, fieldName)
			);
		}

		private void createPureWrapper1(MethodEntity wrapper, string originalName)
		{
			var args = wrapper.GetArgumentTypes(Context);
			var argName = wrapper.Arguments[0].Name;

			var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
			var fieldType = typeof (Dictionary<,>).MakeGenericType(args[0], wrapper.ReturnType);

			CreateField(fieldName, fieldType, true);

			wrapper.Body = Expr.Block(

				// if ($dict == null) $dict = new Dictionary<$argType, $valueType> ()
				Expr.If(
					Expr.Equal(
						Expr.GetMember(EntityNames.MainTypeName, fieldName),
						Expr.Null()
					),
					Expr.Block(
						Expr.SetMember(
							EntityNames.MainTypeName, fieldName,
							Expr.New(fieldType)
						)
					)
				),

				// if(not $dict.ContainsKey key) $dict.Add ($internal arg)
				Expr.If(
					Expr.Not(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"ContainsKey",
							Expr.Get(argName)
						)
					),
					Expr.Block(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"Add",
							Expr.Get(argName),
							Expr.Invoke(EntityNames.MainTypeName, originalName, Expr.Get(argName))
						)
					)
				),

				// $dict[arg]
				Expr.GetIdx(
					Expr.GetMember(EntityNames.MainTypeName, fieldName),
					Expr.Get(argName)
				)
			);
		}

		private void createPureWrapperMany(MethodEntity wrapper, string originalName)
		{
			var args = wrapper.GetArgumentTypes(Context);
			
			var fieldName = string.Format(EntityNames.PureMethodCacheNameTemplate, wrapper.Name);
			var tupleType = FunctionalHelper.CreateTupleType(args);
			var fieldType = typeof(Dictionary<,>).MakeGenericType(tupleType, wrapper.ReturnType);

			CreateField(fieldName, fieldType, true);

			var argGetters = wrapper.Arguments.Select(a => (NodeBase)Expr.Get(a)).ToArray();
			var tupleName = "<args>";
			
			wrapper.Body = Expr.Block(

				// $tmp = new Tuple<...> $arg1 $arg2 ...
				Expr.Let(tupleName, Expr.New(tupleType, argGetters)),

				// if ($dict == null) $dict = new Dictionary<$tupleType, $valueType> ()
				Expr.If(
					Expr.Equal(
						Expr.GetMember(EntityNames.MainTypeName, fieldName),
						Expr.Null()
					),
					Expr.Block(
						Expr.SetMember(
							EntityNames.MainTypeName, fieldName,
							Expr.New(fieldType)
						)
					)
				),

				// if(not $dict.ContainsKey key) $dict.Add ($internal arg)
				Expr.If(
					Expr.Not(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"ContainsKey",
							Expr.Get(tupleName)
						)
					),
					Expr.Block(
						Expr.Invoke(
							Expr.GetMember(EntityNames.MainTypeName, fieldName),
							"Add",
							Expr.Get(tupleName),
							Expr.Invoke(EntityNames.MainTypeName, originalName, argGetters)
						)
					)
				),

				// $dict[arg]
				Expr.GetIdx(
					Expr.GetMember(EntityNames.MainTypeName, fieldName),
					Expr.Get(tupleName)
				)

			);
		}

		#endregion

		#region Structure methods

		/// <summary>
		/// Imports a new method to the given type.
		/// </summary>
		internal void ImportMethod(string name, MethodInfo mi, bool check)
		{
			if(!mi.IsStatic || !mi.IsPublic)
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
				if(_Methods.ContainsKey(name))
					_Methods[name].Add(me);
				else
					_Methods.Add(name, new List<MethodEntity> { me });
			}
		}

		/// <summary>
		/// Creates a new field by type signature.
		/// </summary>
		internal FieldEntity CreateField(string name, TypeSignature signature, bool isStatic = false, bool prepare = false)
		{
			var fe = createFieldCore(name, isStatic, prepare);
			fe.TypeSignature = signature;
			return fe;
		}

		/// <summary>
		/// Creates a new field by resolved type.
		/// </summary>
		internal FieldEntity CreateField(string name, Type type, bool isStatic = false, bool prepare = false)
		{
			var fe = createFieldCore(name, isStatic, prepare);
			fe.Type = type;
			return fe;
		}

		/// <summary>
		/// Creates a new method by resolved argument types.
		/// </summary>
		internal MethodEntity CreateMethod(string name, Type returnType, Type[] argTypes = null, bool isStatic = false, bool isVirtual = false, bool prepare = false)
		{
			var me = createMethodCore(name, isStatic, isVirtual, prepare);
			me.ArgumentTypes = argTypes;
			me.ReturnType = returnType;
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

			var me = createMethodCore(name, isStatic, isVirtual, prepare);
			me.ReturnTypeSignature = returnType;
			me.Arguments = argHash;
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
			};
			_Constructors.Add(ce);

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

		#endregion

		#region Helpers

		/// <summary>
		/// Create a field without setting type info.
		/// </summary>
		private FieldEntity createFieldCore(string name, bool isStatic, bool prepare)
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

			if(prepare)
				fe.PrepareSelf();

			return fe;
		}

		/// <summary>
		/// Creates a method without setting argument type info.
		/// </summary>
		private MethodEntity createMethodCore(string name, bool isStatic, bool isVirtual, bool prepare)
		{
			var me = new MethodEntity
			{
				Name = name,
				IsStatic = isStatic,
				IsVirtual = isVirtual,
				ContainerType = this,
			};

			_MethodList.Add(me);

			if(prepare)
				me.PrepareSelf();

			return me;
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