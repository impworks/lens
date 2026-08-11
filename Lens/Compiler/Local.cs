using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.SyntaxTree;

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
        public Local(string name, Type type, bool isConst = false, bool isRefArg = false)
        {
            Name = name;
            Type = type;
            IsImmutable = isConst;
            IsRefArgument = isRefArg;
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
        public readonly Type Type;

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