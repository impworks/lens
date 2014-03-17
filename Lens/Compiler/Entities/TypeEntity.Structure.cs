using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler.Entities
{
	internal partial class TypeEntity
	{
		#region Methods

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
			var me = createMethodCore(name, isStatic, isVirtual, prepare);
			me.ReturnTypeSignature = returnType;
			me.Arguments = new HashList<FunctionArgument>(args, x => x.Name);
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

			if (prepare)
				ce.PrepareSelf();

			return ce;
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

			if (prepare)
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

			if (prepare)
				me.PrepareSelf();

			return me;
		}

		#endregion
	}
}
