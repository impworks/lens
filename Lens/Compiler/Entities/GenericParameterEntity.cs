using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Lens.Compiler.Entities
{
    /// <summary>
    /// The compiler's own model of a generic parameter declared in LENS code.
    ///
    /// The constraints cannot be reliably read back from a <see cref="GenericTypeParameterBuilder"/>
    /// while it is still being built, so they are kept here and consulted both when validating
    /// type arguments and when calculating type distance.
    /// </summary>
    internal class GenericParameterEntity
    {
        #region Constructor

        public GenericParameterEntity(string name, int ordinal, string declarationName)
        {
            Name = name;
            Ordinal = ordinal;
            DeclarationName = declarationName;

            TypeConstraintSignatures = new List<TypeSignature>();
            Interfaces = new List<Type>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name of the parameter as written in LENS code.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The position of the parameter in its declaration's parameter list.
        /// </summary>
        public readonly int Ordinal;

        /// <summary>
        /// The name of the function or type that declares the parameter. For diagnostics only.
        /// </summary>
        public readonly string DeclarationName;

        /// <summary>
        /// The parameter is constrained to reference types ("class").
        /// </summary>
        public bool IsReferenceType;

        /// <summary>
        /// The parameter is constrained to non-nullable value types ("struct").
        /// </summary>
        public bool IsValueType;

        /// <summary>
        /// The parameter is constrained to types with a public parameterless constructor ("new").
        /// </summary>
        public bool RequiresDefaultCtor;

        /// <summary>
        /// The signatures of all type constraints, in declaration order. They are only
        /// separated into a base type and interfaces once they have been resolved, because
        /// a signature can name a sibling parameter.
        /// </summary>
        public readonly List<TypeSignature> TypeConstraintSignatures;

        /// <summary>
        /// The resolved base type constraint, if any.
        /// </summary>
        public Type BaseType;

        /// <summary>
        /// The resolved interface constraints.
        /// </summary>
        public readonly List<Type> Interfaces;

        /// <summary>
        /// The builder that represents this parameter in the emitted assembly.
        /// </summary>
        public GenericTypeParameterBuilder Builder;

        /// <summary>
        /// The parameter this one forwards, when it belongs to a compiler-generated type that has
        /// to be generic in the parameters of its enclosing declaration.
        /// </summary>
        public GenericParameterEntity Source;

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"{DeclarationName}<{Name}>";
        }

        #endregion
    }
}
