using System;
using System.Collections.Generic;
using Lens.Resolver;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Fields

        /// <summary>
        /// The safe mode restrictions of this compilation, compiled out of the options.
        /// </summary>
        private SafeModeRules _safeModeRules;

        #endregion

        #region Methods

        /// <summary>
        /// Compiles the safe mode restrictions the options describe.
        /// </summary>
        private void InitSafeMode()
        {
            _safeModeRules = SafeModeRules.From(Options);
        }

        /// <summary>
        /// Checks if the type is allowed according to the safe mode restrictions.
        ///
        /// A type is more than its name, and the parts it is built out of are checked before the
        /// name is: an array is as allowed as its element type, a constructed generic as its
        /// definition and every one of its arguments.
        /// </summary>
        public bool IsTypeAllowed(TypeEntry type)
        {
            if (_safeModeRules.Mode == SafeMode.Disabled)
                return true;

            if (ReferenceEquals(type, null))
                return true;

            // A generic parameter stands for a type rather than being one, so there is nothing here
            // for a rule to name: 'T' is whatever it is substituted with, and that substitution is
            // what the generic argument check below asks about. Deciding it on its own would refuse
            // every generic function and record under a whitelist, and it has no full name to look
            // up under either mode.
            if (type.IsGenericParameter)
                return true;

            // T[], ref T and T* are the same question about T, asked one level up. Answering it on
            // the outer name instead would miss the type entirely: the full name of File[] is
            // 'System.IO.File[]', which is not the name any rule about File is written with.
            var element = type.ElementType;
            if (!ReferenceEquals(element, null))
                return IsTypeAllowed(element);

            // a type the script declared is the script's own; the host types its fields and methods
            // are built out of are checked where they appear, which is what the rules are about
            if (type.IsDeclared)
                return true;

            foreach (var curr in type.GenericArguments)
                if (!IsTypeAllowed(curr))
                    return false;

            // List<int> carries its arguments in its own full name, so it is the definition that
            // has the name a rule is written with
            var definition = type.GetGenericDefinition();
            var fullName = (ReferenceEquals(definition, null) ? type : definition).FullName ?? type.FullName;

            return _safeModeRules.IsNameAllowed(fullName, type.Namespace);
        }

        /// <summary>
        /// Checks if a CLR type is allowed according to the safe mode restrictions.
        ///
        /// The same question as the overload above, asked of a type that has not been modelled yet:
        /// a completion list weighs every exported type of every referenced assembly, and building a
        /// <see cref="TypeEntry"/> for each of them only to throw it away is work nobody needs.
        /// </summary>
        internal bool IsTypeAllowed(Type type)
        {
            if (_safeModeRules.Mode == SafeMode.Disabled)
                return true;

            if (type == null)
                return true;

            if (type.IsGenericParameter)
                return true;

            if (type.HasElementType)
                return IsTypeAllowed(type.GetElementType());

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var curr in type.GetGenericArguments())
                    if (!IsTypeAllowed(curr))
                        return false;

                type = type.GetGenericTypeDefinition();
            }

            return _safeModeRules.IsNameAllowed(type.FullName, type.Namespace);
        }

        /// <summary>
        /// Checks if a member may be named by the script.
        ///
        /// This is the question the type-level rules cannot answer. Type.GetType takes the name of
        /// a type as a string and hands back a type, which means no rule about which types may be
        /// named ever sees what came out of it; the only place to stop that is the call itself.
        ///
        /// Member rules subtract from what the type rules allowed and never add to it, in both
        /// modes - a whitelist of members would mean naming every method a script is allowed to
        /// call, which is not a list anyone maintains correctly.
        /// </summary>
        internal bool IsMemberAllowed(WrapperBase member)
        {
            if (_safeModeRules.Mode == SafeMode.Disabled || member == null)
                return true;

            return _safeModeRules.IsMemberAllowed(InheritanceNames(member.DeclaringType), member.Name);
        }

        /// <summary>
        /// The names of a type and of everything it inherits from, so that a rule about
        /// Object.GetType covers every expression the call can be written on.
        /// </summary>
        private static IEnumerable<string> InheritanceNames(TypeEntry type)
        {
            var curr = type;
            while (!ReferenceEquals(curr, null))
            {
                var definition = curr.GetGenericDefinition();
                yield return (ReferenceEquals(definition, null) ? curr : definition).FullName;

                curr = curr.BaseType;
            }
        }

        #endregion
    }
}
