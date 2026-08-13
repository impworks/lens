using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Operators.TypeBased;
using Lens.Translations;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// A node representing a new object creation.
    /// </summary>
    internal class NewObjectNode : InvocationNodeBase
    {
        #region Constructor

        public NewObjectNode(string type = null)
        {
            TypeSignature = type;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The type of the object to create.
        /// </summary>
        public TypeEntry Type;

        /// <summary>
        /// The type signature of the object to create.
        /// </summary>
        public TypeSignature TypeSignature;

        #endregion

        #region Binding

        /// <summary>
        /// What binding learned about this object creation.
        /// </summary>
        private class Binding : InvocationBinding
        {
            /// <summary>
            /// The constructor the creation resolved to.
            /// </summary>
            public ConstructorWrapper Constructor;

            /// <summary>
            /// Indicates the constructor call may be replaced with 'default' initialization.
            /// </summary>
            public bool IsDefault;

            /// <summary>
            /// Indicates the created type is a generic parameter with the 'new' constraint.
            /// </summary>
            public bool IsTypeParameter;
        }

        protected override InvocationBinding GetBinding(Context ctx)
        {
            return ctx.BindingOf<Binding>(this);
        }

        protected override CallableWrapperBase GetWrapper(Context ctx)
        {
            return ctx.BindingOf<Binding>(this).Constructor;
        }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            base.ResolveInternal(ctx, true);

            var binding = ctx.BindingOf<Binding>(this);
            var type = Type;

            try
            {
                type ??= ctx.ResolveType(TypeSignature);
            }
            catch (TypeMatchException ex)
            {
                Error(ex.Message, TypeSignature.FullSignature);
            }

            if (type.IsVoid())
                Error(CompilerMessages.VoidTypeDefault);

            if (type.IsAbstract)
                Error(CompilerMessages.TypeAbstract, TypeSignature.FullSignature);

            if (type.IsInterface)
                Error(CompilerMessages.TypeInterface, TypeSignature.FullSignature);

            if (Arguments.Count == 0)
                Error(CompilerMessages.ParameterlessConstructorParens);

            // a type parameter has no constructors of its own: the 'new' constraint promises a
            // parameterless one, and the CLI reaches it through Activator.CreateInstance<T>
            if (type.IsGenericParameter)
            {
                var constraints = ctx.Resolver.FindConstraints(type);
                if (binding.ArgTypes.Length > 0 || constraints == null || !constraints.RequiresDefaultCtor)
                    Error(CompilerMessages.TypeConstructorNotFound, type);

                binding.IsTypeParameter = true;
                return type;
            }

            try
            {
                binding.Constructor = ctx.ResolveConstructor(type, binding.ArgTypes);
            }
            catch (TypeMatchException ex)
            {
                Error(ex.Message, TypeSignature.FullSignature);
            }
            catch (AmbiguousMatchException)
            {
                Error(CompilerMessages.TypeConstructorAmbiguos, TypeSignature.FullSignature);
            }
            catch (KeyNotFoundException)
            {
                if (binding.ArgTypes.Length > 0 || !type.IsValueType)
                    Error(CompilerMessages.TypeConstructorNotFound, TypeSignature.FullSignature);

                binding.IsDefault = true;
                return type;
            }

            ApplyLambdaArgTypes(ctx);

            return ResolvePartial(binding.Constructor, type, binding.ArgTypes);
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var binding = ctx.BindingOf<Binding>(this);

            // there is no constructor wrapper to partially apply
            if (binding.IsTypeParameter)
                return null;

            if (binding.IsDefault)
                return new DefaultOperatorNode {Type = Type, TypeSignature = TypeSignature};

            return base.Expand(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var binding = ctx.BindingOf<Binding>(this);

            if (binding.IsTypeParameter)
            {
                var type = Type ?? ctx.ResolveType(TypeSignature);
                var creator = typeof(Activator).GetMethod("CreateInstance", System.Type.EmptyTypes).MakeGenericMethod(type.Materialize());
                gen.EmitCall(creator);
                return;
            }

            if (binding.Constructor != null)
            {
                if (binding.ArgTypes.Length > 0)
                {
                    var destTypes = binding.Constructor.ArgumentTypes;
                    for (var idx = 0; idx < binding.Arguments.Count; idx++)
                        Expr.Cast(binding.Arguments[idx], destTypes[idx].Materialize()).Emit(ctx, true);
                }

                gen.EmitCreateObject(binding.Constructor.ConstructorInfo);
            }
            else
            {
                Expr.Default(TypeSignature).Emit(ctx, true);
            }
        }

        #endregion

        #region Helpers

        protected override InvocationNodeBase RecreateSelfWithArgs(IEnumerable<NodeBase> newArgs)
        {
            return new NewObjectNode {Type = Type, TypeSignature = TypeSignature, Arguments = newArgs.ToList()};
        }

        #endregion

        #region Debug

        protected bool Equals(NewObjectNode other)
        {
            return base.Equals(other) && Equals(TypeSignature, other.TypeSignature);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NewObjectNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("new({0}, args: {1})", TypeSignature.FullSignature, string.Join(";", Arguments));
        }

        #endregion
    }
}