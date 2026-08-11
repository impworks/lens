using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Resolver;
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
            var me = new MethodEntity(this)
            {
                Name = name,
                IsImported = true,
                IsStatic = true,
                IsVirtual = false,
                IsVariadic = ReflectionHelper.IsVariadic(mi),
                MethodInfo = mi,
                ReturnType = mi.ReturnType,
                Arguments = new HashList<FunctionArgument>(args, arg => arg.Name)
            };

            if (check)
            {
                CheckMethod(me);
            }
            else
            {
                if (_methods.ContainsKey(name))
                    _methods[name].Add(me);
                else
                    _methods.Add(name, new List<MethodEntity> {me});
            }
        }

        /// <summary>
        /// Creates a new field by type signature.
        /// </summary>
        internal FieldEntity CreateField(string name, TypeSignature signature, bool isStatic = false, bool prepare = true)
        {
            return CreateFieldCore(name, isStatic, prepare, fe => fe.TypeSignature = signature);
        }

        /// <summary>
        /// Creates a new field by resolved type.
        /// </summary>
        internal FieldEntity CreateField(string name, Type type, bool isStatic = false, bool prepare = true)
        {
            return CreateFieldCore(name, isStatic, prepare, fe => fe.Type = type);
        }

        /// <summary>
        /// Creates a new method by resolved argument types.
        /// </summary>
        internal MethodEntity CreateMethod(string name, Type returnType, Type[] argTypes = null, bool isStatic = false, bool isVirtual = false, bool prepare = true)
        {
            return CreateMethodCore(name, isStatic, isVirtual, prepare, me =>
                {
                    me.ArgumentTypes = argTypes;
                    me.ReturnType = returnType;
                }
            );
        }

        /// <summary>
        /// Creates a new method with argument types given by signatures.
        /// </summary>
        internal MethodEntity CreateMethod(string name, TypeSignature returnType, string[] argTypes = null, bool isStatic = false, bool isVirtual = false, bool prepare = true)
        {
            var args = argTypes?.Select((a, idx) => new FunctionArgument("arg" + idx.ToString(), a)).ToArray();

            return CreateMethod(name, returnType, args, isStatic, isVirtual, prepare);
        }

        /// <summary>
        /// Creates a new method with argument types given by function arguments.
        /// </summary>
        internal MethodEntity CreateMethod(string name, TypeSignature returnType, IEnumerable<FunctionArgument> args = null, bool isStatic = false, bool isVirtual = false, bool prepare = true)
        {
            return CreateMethodCore(name, isStatic, isVirtual, prepare, me =>
                {
                    me.Arguments = new HashList<FunctionArgument>(args, x => x.Name);
                    me.ReturnTypeSignature = returnType;
                }
            );
        }

        /// <summary>
        /// Creates a new method with a resolved return type and argument types given by function arguments.
        /// </summary>
        internal MethodEntity CreateMethod(string name, Type returnType, IEnumerable<FunctionArgument> args, bool isStatic = false, bool isVirtual = false, bool prepare = true)
        {
            return CreateMethodCore(name, isStatic, isVirtual, prepare, me =>
                {
                    me.Arguments = new HashList<FunctionArgument>(args, x => x.Name);
                    me.ReturnType = returnType;
                }
            );
        }

        /// <summary>
        /// Creates a new constructor with the given argument types.
        /// </summary>
        internal ConstructorEntity CreateConstructor(string[] argTypes = null, bool prepare = true)
        {
            var ce = new ConstructorEntity(this)
            {
                ArgumentTypes = argTypes?.Select(x => Context.ResolveType(x).Materialize()).ToArray(),
            };

            _constructors.Add(ce);
            Context.UnprocessedMethods.Add(ce);

            if (prepare)
                ce.PrepareSelf();
            else
                Context.UnpreparedTypeContents.Add(ce);

            return ce;
        }

        /// <summary>
        /// Invokes generation of FieldBuilder, MethodBuilder and ConstructorBuilder objects for type members.
        /// </summary>
        public void CheckMethod(MethodEntity method)
        {
            CheckGenericArity(method);

            try
            {
                // exception is good
                ResolveMethod(method.Name, method.GetArgumentTypes(Context), true);

                if (this == Context.MainType)
                    Context.Error(CompilerMessages.FunctionRedefinition, method.Name);
                else
                    Context.Error(CompilerMessages.MethodRedefinition, method.Name, Name);
            }
            catch (KeyNotFoundException)
            {
                if (!_methods.ContainsKey(method.Name))
                    _methods.Add(method.Name, new List<MethodEntity> {method});
                else
                    _methods[method.Name].Add(method);
            }
        }

        /// <summary>
        /// Forbids one name from being declared both generic and non-generic, or generic with two
        /// different arities. C# permits it; LENS does not, because it buys nothing for glue code
        /// and makes lookup and error messages materially worse.
        /// </summary>
        private void CheckGenericArity(MethodEntity method)
        {
            if (!_methods.TryGetValue(method.Name, out var group))
                return;

            var argCount = method.GetArgumentTypes(Context).Length;

            foreach (var curr in group)
            {
                if (curr.GetArgumentTypes(Context).Length == argCount && curr.GenericParameterCount != method.GenericParameterCount)
                    Context.Error(CompilerMessages.GenericArityOverloading, method.Name);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Create a field without setting type info.
        /// </summary>
        private FieldEntity CreateFieldCore(string name, bool isStatic, bool prepare, Action<FieldEntity> extraInit = null)
        {
            if (_fields.ContainsKey(name))
                Context.Error(CompilerMessages.FieldRedefinition, Name, name);

            var fe = new FieldEntity(this)
            {
                Name = name,
                IsStatic = isStatic,
            };

            _fields.Add(name, fe);

            extraInit?.Invoke(fe);

            if (prepare)
                fe.PrepareSelf();
            else
                Context.UnpreparedTypeContents.Add(fe);

            return fe;
        }

        /// <summary>
        /// Creates a method without setting argument type info.
        /// </summary>
        private MethodEntity CreateMethodCore(string name, bool isStatic, bool isVirtual, bool prepare, Action<MethodEntity> extraInit = null)
        {
            var me = new MethodEntity(this)
            {
                Name = name,
                IsStatic = isStatic,
                IsVirtual = isVirtual,
            };

            Context.UnprocessedMethods.Add(me);

            extraInit?.Invoke(me);

            if (prepare)
            {
                me.PrepareSelf();
                CheckMethod(me);
            }
            else
            {
                Context.UnpreparedTypeContents.Add(me);
            }

            return me;
        }

        #endregion
    }
}