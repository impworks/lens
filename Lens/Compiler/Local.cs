using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// A class representing info about a local variable.
    /// </summary>
    internal class Local
    {
        #region Constructors

        /// <summary>
        /// Creates a new instance of the Local variable.
        /// </summary>
        public Local(string name, TypeEntry type, bool isConst = false, bool isRefArg = false)
        {
            Name = name;
            Type = type;
            IsImmutable = isConst;
            IsRefArgument = isRefArg;

            // a name declared without a type is one whose type is still being worked out from the
            // values assigned to it
            IsTypeDeferred = type == null;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Variable name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Variable type.
        /// </summary>
        public TypeEntry Type { get; private set; }

        /// <summary>
        /// Whether the type is still being worked out from the values assigned to the name.
        ///
        /// The lowering pass runs before anything has a type, because a state machine is built out
        /// of the parse tree. When it has to invent a name to hold the value of a branch - what a
        /// suspending 'if' or 'match' produces - there is nothing it can write down as the type,
        /// and nothing it could resolve either, since the branch bodies mention names that only
        /// come into being while the construct around them is bound. So the name is declared
        /// without one, and each assignment widens it until the first read settles it. Binding
        /// reaches the assignments before the read, in the order the statements were written.
        /// </summary>
        public bool IsTypeDeferred { get; private set; }

        /// <summary>
        /// Is the name a constant or a variable?
        /// </summary>
        public readonly bool IsImmutable;

        /// <summary>
        /// Does the variable represent a function argument that is passed by ref?
        /// </summary>
        public readonly bool IsRefArgument;

        /// <summary>
        /// The ID of the argument if this name represents one.
        /// </summary>
        public int? ArgumentId;

        /// <summary>
        /// Is the name referenced in nested scopes?
        /// </summary>
        public bool IsClosured;

        /// <summary>
        /// The name of the field in closured class.
        /// </summary>
        public string ClosureFieldName;

        /// <summary>
        /// The scope that owns the closure type in which the variable's field is declared.
        /// </summary>
        public Scope ClosureScope;

        /// <summary>
        /// The local builder identifier.
        /// </summary>
        public LocalBuilder LocalBuilder;

        /// <summary>
        /// Checks if the current local name represents a constant.
        /// Must also be immutable!
        /// </summary>
        public bool IsConstant;

        /// <summary>
        /// The compile-time constant value for current local name.
        /// </summary>
        public dynamic ConstantValue;

        #endregion

        #region Deferred type

        /// <summary>
        /// Widens the name's type to also hold what is being assigned to it.
        /// </summary>
        public void ContributeType(TypeResolutionContext ctx, TypeEntry type)
        {
            if (!IsTypeDeferred)
                throw new InvalidOperationException($"The type of '{Name}' is not deferred.");

            Type = Type == null
                ? type
                : new[] {Type, type}.GetMostCommonType(ctx);
        }

        /// <summary>
        /// Settles the name's type, because something is about to read it. Every assignment that
        /// was going to widen it has been bound by now: they were all written before the read.
        /// </summary>
        public void SealType()
        {
            if (!IsTypeDeferred)
                return;

            if (Type == null)
                throw new LensCompilerException(string.Format(CompilerMessages.DeferredNameNeverAssigned, Name));

            IsTypeDeferred = false;
        }

        #endregion

        #region Symbol identity

        /// <summary>
        /// Where the variable was declared. Null for the variables the compiler invents.
        /// </summary>
        public LocationEntity Declaration;

        /// <summary>
        /// Every place in the source that names this variable, in the order binding met them.
        ///
        /// This is what turns "some local called x" into "the variable x declared on line 12", and
        /// it is the difference between a rename that works and a text search.
        /// </summary>
        public readonly List<LocationEntity> References = new List<LocationEntity>();

        /// <summary>
        /// Records a place that names this variable.
        /// </summary>
        public void Reference(LocationEntity entity)
        {
            // only the source names a variable: the nodes the compiler synthesises while expanding
            // carry no location and are not somewhere anyone can navigate to
            if (entity == null || (entity.StartLocation.Line == 0 && entity.StartLocation.Offset == 0))
                return;

            // by identity, not by Equals: syntax tree nodes compare structurally, so the two
            // mentions of 'n' in 'n + n' are equal to each other and are still two references
            foreach (var curr in References)
            {
                if (ReferenceEquals(curr, entity))
                    return;
            }

            References.Add(entity);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            var entities = new List<string>();

            if (IsClosured) entities.Add("closured");
            if (IsRefArgument) entities.Add("ref");
            if (IsImmutable) entities.Add("immutable");
            if (IsConstant) entities.Add("const");
            if (ArgumentId != null) entities.Add($"arg({ArgumentId})");

            return string.Format(
                "{0}:{1} ({2})",
                Name,
                Type.Name,
                string.Join(", ", entities)
            );
        }

        #endregion
    }
}