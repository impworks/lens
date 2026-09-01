using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Resolver
{
    /// <summary>
    /// A collection of helpful methods for types.
    /// </summary>
    internal static class TypeExtensions
    {
        #region Static constructor

        static TypeExtensions()
        {
            SignedIntegerTypes = new[]
            {
                TypeEntryCache.Of<sbyte>(),
                TypeEntryCache.Of<short>(),
                TypeEntryCache.Of<int>(),
                TypeEntryCache.Of<long>()
            };

            UnsignedIntegerTypes = new[]
            {
                TypeEntryCache.Of<byte>(),
                TypeEntryCache.Of<ushort>(),
                TypeEntryCache.Of<uint>(),
                TypeEntryCache.Of<ulong>(),
            };

            FloatTypes = new[]
            {
                TypeEntryCache.Of<float>(),
                TypeEntryCache.Of<double>(),
                TypeEntryCache.Of<decimal>()
            };
        }

        #endregion

        #region Fields

        public static readonly TypeEntry[] SignedIntegerTypes;
        public static readonly TypeEntry[] UnsignedIntegerTypes;
        public static readonly TypeEntry[] FloatTypes;

        #endregion

        #region Type class checking

        /// <summary>
        /// Checks if a type is a <see cref="Nullable{T}"/>.
        /// </summary>
        /// <param name="type">Checked type.</param>
        /// <returns><c>true</c> if type is a <see cref="Nullable{T}"/>.</returns>
        public static bool IsNullableType(this TypeEntry type)
        {
            return type.IsGenericType && type.GetGenericDefinition().Is(typeof(Nullable<>));
        }

        /// <summary>
        /// Checks if a type is a signed integer type.
        /// </summary>
        public static bool IsSignedIntegerType(this TypeEntry type)
        {
            return SignedIntegerTypes.Contains(type);
        }

        /// <summary>
        /// Checks if a type is an unsigned integer type.
        /// </summary>
        public static bool IsUnsignedIntegerType(this TypeEntry type)
        {
            return UnsignedIntegerTypes.Contains(type);
        }

        /// <summary>
        /// Checks if a type is a floating point type.
        /// </summary>
        public static bool IsFloatType(this TypeEntry type)
        {
            return FloatTypes.Contains(type);
        }

        /// <summary>
        /// Checks if a type is any of integer types, signed or unsigned.
        /// </summary>
        public static bool IsIntegerType(this TypeEntry type)
        {
            return type.IsSignedIntegerType() || type.IsUnsignedIntegerType();
        }

        /// <summary>
        /// Checks if a type is any of the numeric types.
        /// </summary>
        public static bool IsNumericType(this TypeEntry type, bool allowNonPrimitives = false)
        {
            if (!allowNonPrimitives && type.Is<decimal>())
                return false;

            return type.IsSignedIntegerType() || type.IsUnsignedIntegerType() || type.IsFloatType();
        }

        /// <summary>
        /// Checks if the type is void.
        /// </summary>
        public static bool IsVoid(this TypeEntry type)
        {
            return type.Is(typeof(void)) || type.Is<UnitType>();
        }

        /// <summary>
        /// Checks if the type is a struct.
        /// </summary>
        public static bool IsStruct(this TypeEntry type)
        {
            return type.IsValueType && !type.IsNumericType();
        }

        /// <summary>
        /// Checks if type is actually boolean or can be implicitly casted to boolean.
        /// </summary>
        public static bool IsImplicitlyBoolean(this TypeEntry type)
        {
            return type.Is<bool>() || type.Materialize().GetMethods().Any(m => m.Name == "op_Implicit" && m.ReturnType == typeof(bool));
        }

        /// <summary>
        /// Returns T for Nullable&lt;T&gt;, or null if the type is not Nullable.
        /// </summary>
        public static TypeEntry GetNullableUnderlyingType(this TypeEntry type)
        {
            return type.IsNullableType() ? type.GenericArguments[0] : null;
        }

        #endregion

        #region Type distance

        /// <summary>
        /// Checks if a variable of given type can be assigned from other type (including type extension).
        /// </summary>
        /// <param name="varType">Type of assignment target (ex. variable)</param>
        /// <param name="exprType">Type of assignment source (ex. expression)</param>
        /// <param name="exactly">Checks whether types must be compatible as-is, or additional code may be implicitly issued by the compiler.</param>
        /// <returns></returns>
        public static bool IsExtendablyAssignableFrom(this TypeEntry varType, TypeResolutionContext ctx, TypeEntry exprType, bool exactly = false)
        {
            return varType.DistanceFrom(ctx, exprType, exactly) < int.MaxValue;
        }

        /// <summary>
        /// Gets distance between two types.
        /// This method is memoized within the current compilation.
        /// </summary>
        public static int DistanceFrom(this TypeEntry varType, TypeResolutionContext ctx, TypeEntry exprType, bool exactly = false)
        {
            // a declaration is never memoized anyway - its shape can still change - and asking one
            // for a CLR type would create the very assembly artefact that analysis must not need,
            // so those pairs go straight to the calculation
            if (IsDeclaration(varType) || IsDeclaration(exprType))
                return distanceFrom(ctx, varType, exprType, exactly);

            // the memoization key is the pair of CLR types: entries are canonical per type, so the
            // two keyings agree, and the cache has to decide whether a type is still being built
            return ctx.CachedDistance(varType?.Materialize(), exprType?.Materialize(), exactly, () => distanceFrom(ctx, varType, exprType, exactly));
        }

        /// <summary>
        /// Checks whether an entry stands for something the script declared, or is built out of one:
        /// a type entity, a generic parameter, T[], List&lt;SomeRecord&gt;.
        /// </summary>
        private static bool IsDeclaration(TypeEntry type)
        {
            return !ReferenceEquals(type, null) && type.ContainsDeclared;
        }

        /// <summary>
        /// Calculates the distance between two types.
        /// </summary>
        private static int distanceFrom(TypeResolutionContext ctx, TypeEntry varType, TypeEntry exprType, bool exactly = false)
        {
            if (varType == exprType)
                return 0;

            // partial application
            if (exprType.Is<UnspecifiedType>())
                return 0;

            // the absence of a value fits nothing but the absence of a value: Unit is an ordinary
            // class as far as the CLR is concerned, so without this it would reach 'object' by
            // inheritance - and through it any generic parameter, whose base is object as well
            var varIsVoid = !ReferenceEquals(varType, null) && varType.IsVoid();
            var exprIsVoid = !ReferenceEquals(exprType, null) && exprType.IsVoid();
            if (varIsVoid || exprIsVoid)
                return varIsVoid && exprIsVoid ? 0 : int.MaxValue;

            if (varType.IsByRef)
                return varType.ElementType == exprType ? 0 : int.MaxValue;

            if (!exactly)
            {
                if (varType.IsNullableType() && exprType == varType.GetNullableUnderlyingType())
                    return 1;

                if ((varType.IsClass || varType.IsNullableType()) && exprType.Is<NullType>())
                    return 1;

                if (varType.IsNumericType(true) && exprType.IsNumericType(true))
                    return NumericTypeConversion(varType, exprType);
            }

            if (varType.Is<object>())
            {
                if (exprType.IsValueType)
                    return exactly ? int.MaxValue : 1;

                if (exprType.IsInterface)
                    return 1;
            }

            if (varType.IsInterface)
            {
                var idist = InterfaceDistance(ctx, varType, exprType.GetInterfaces(ctx));
                if (idist != int.MaxValue)
                    return idist;
            }

            if (varType.IsGenericParameter || exprType.IsGenericParameter)
                return GenericParameterDistance(ctx, varType, exprType);

            // a lambda reaches a parameter that wants an expression tree exactly as it reaches one
            // that wants the delegate: which of the two a method asks for is what distinguishes
            // Queryable from Enumerable, and the receiver decides between them
            if (varType.IsExpressionType() && (exprType.IsLambdaType() || exprType.IsCallableType()))
                return varType.GenericArguments[0].DistanceFrom(ctx, exprType, exactly);

            // a lambda literal reaches a parameter that wants a delegate by matching its signature.
            // One that wants anything else - 'object', most often - takes it as the ordinary
            // reference it is, which is what the inheritance walk below answers
            if (exprType.IsLambdaType() && varType.IsCallableType())
                return LambdaDistance(ctx, varType, exprType);

            if (varType.IsGenericType && exprType.IsGenericType)
            {
                // note that 'exactly' is deliberately not forwarded: the arguments of a generic
                // type have always been compared leniently
                var genericDistance = GenericDistance(ctx, varType, exprType);
                if (genericDistance != int.MaxValue)
                    return genericDistance;

                // a label of a generic algebraic type is related to the type itself through
                // inheritance and instantiation at once, so the chain has to be searched for an
                // ancestor that matches by instantiation.
                //
                // This only applies to types declared in the script: for imported generics the
                // long-standing behaviour of stopping right here is preserved.
                if (IsDeclaredGeneric(varType))
                {
                    var current = GetBaseType(exprType);
                    var steps = 1;
                    while (current != null)
                    {
                        if (current.IsGenericType)
                        {
                            genericDistance = GenericDistance(ctx, varType, current);
                            if (genericDistance != int.MaxValue)
                                return steps + genericDistance;
                        }

                        current = GetBaseType(current);
                        steps++;
                    }
                }
            }

            if (IsDerivedFrom(exprType, varType, out int result))
                return result;

            if (varType.IsArray && exprType.IsArray)
            {
                var varElType = varType.ElementType;
                var exprElType = exprType.ElementType;

                var areRefs = !varElType.IsValueType && !exprElType.IsValueType;
                var generic = varElType.IsGenericParameter || exprElType.IsGenericParameter;
                if (areRefs || generic)
                    return varElType.DistanceFrom(ctx, exprElType, exactly);
            }

            return int.MaxValue;
        }

        /// <summary>
        /// Calculates the distance to any of given interfaces.
        /// </summary>
        private static int InterfaceDistance(TypeResolutionContext ctx, TypeEntry interfaceType, IEnumerable<TypeEntry> ifaces, bool exactly = false)
        {
            var min = int.MaxValue;
            foreach (var iface in ifaces)
            {
                if (iface == interfaceType)
                    return 1;

                if (interfaceType.IsGenericType && iface.IsGenericType)
                {
                    var dist = GenericDistance(ctx, interfaceType, iface, exactly);
                    if (dist < min)
                        min = dist;
                }
            }

            return min;
        }

        /// <summary>
        /// Checks if a type is a child for some other type.
        /// </summary>
        private static bool IsDerivedFrom(TypeEntry derivedType, TypeEntry baseType, out int distance)
        {
            distance = 0;
            var current = derivedType;
            while (current != null && current != baseType)
            {
                current = GetBaseType(current);
                ++distance;
            }

            return current == baseType;
        }

        /// <summary>
        /// Checks if a constructed generic type was declared in the script.
        /// </summary>
        private static bool IsDeclaredGeneric(TypeEntry type)
        {
            // the definition behind the instantiation says whether the script declared it, with no
            // need to materialize anything and no need for it to have been emitted yet
            return !type.IsGenericTypeDefinition && type.GenericDefinition?.IsDeclared == true;
        }

        /// <summary>
        /// Gets the base type of a type, tolerating entities that are still being built.
        /// </summary>
        private static TypeEntry GetBaseType(TypeEntry type)
        {
            return type.BaseType;
        }

        /// <summary>
        /// Calculates compound distance of two generic types' arguments if applicable.
        /// </summary>
        private static int GenericDistance(TypeResolutionContext ctx, TypeEntry varType, TypeEntry exprType, bool exactly = false)
        {
            var definition = varType.GetGenericDefinition();
            if (definition != exprType.GetGenericDefinition())
                return int.MaxValue;

            var arguments = definition.GenericArguments;
            var arguments1 = varType.GenericArguments;
            var arguments2 = exprType.GenericArguments;

            var result = 0;
            for (var i = 0; i < arguments1.Length; ++i)
            {
                var argument1 = arguments1[i];
                var argument2 = arguments2[i];
                if (argument1 == argument2)
                    continue;

                var argument = arguments[i];
                var attributes = GetGenericParameterAttributes(argument);

                int conversionResult;
                if (argument1.IsGenericParameter)
                {
                    // generic parameter may be substituted with anything
                    // including value types
                    conversionResult = GenericParameterDistance(ctx, argument1, argument2, exactly);
                }
                else if (argument2.IsGenericParameter)
                {
                    conversionResult = GenericParameterDistance(ctx, argument2, argument1, exactly);
                }
                else if (attributes.HasFlag(GenericParameterAttributes.Contravariant))
                {
                    // generic variance applies to ref-types only
                    if (argument1.IsValueType)
                        return int.MaxValue;

                    // dist(X<in T1>, X<in T2>) = dist(T2, T1)
                    conversionResult = argument2.DistanceFrom(ctx, argument1, exactly);
                }
                else if (attributes.HasFlag(GenericParameterAttributes.Covariant))
                {
                    if (argument2.IsValueType)
                        return int.MaxValue;

                    // dist(X<out T1>, X<out T2>) = dist(T1, T2)
                    conversionResult = argument1.DistanceFrom(ctx, argument2, exactly);
                }
                else if (argument1.IsGenericType && argument2.IsGenericType)
                {
                    // nested generic types
                    conversionResult = GenericDistance(ctx, argument1, argument2, exactly);
                }
                else
                {
                    // No possible conversion found.
                    return int.MaxValue;
                }

                if (conversionResult == int.MaxValue)
                    return int.MaxValue;

                checked
                {
                    result += conversionResult;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a type can be used as a substitute for a generic parameter.
        /// </summary>
        private static int GenericParameterDistance(TypeResolutionContext ctx, TypeEntry varType, TypeEntry exprType, bool exactly = false)
        {
            // generic parameter is on the same level of inheritance as the expression
            // therefore getting its parent type does not take a step
            var dist = varType.IsGenericParameter
                // a value is being stored into the parameter: whether it actually fits is decided
                // by constraint checking, which produces a far better diagnostic than silently
                // dropping the candidate from overload resolution
                ? DistanceFrom(GetGenericParameterBase(ctx, varType, true), ctx, exprType, exactly)
                : DistanceFrom(GetGenericParameterBase(ctx, exprType, false), ctx, varType, exactly);

            return dist == int.MaxValue ? dist : dist + 1;
        }

        /// <summary>
        /// Gets the effective base type of a generic parameter.
        /// For LENS-declared parameters the constraint model is used, because the constraints
        /// cannot be read back from an unfinished builder.
        /// </summary>
        private static TypeEntry GetGenericParameterBase(TypeResolutionContext ctx, TypeEntry type, bool ignoreConstraints)
        {
            // a declared parameter carries its own constraints, and its BaseType already applies the
            // CLI defaults - ValueType for a struct constraint, object otherwise
            if (type.IsGenericParameter && type.IsDeclared)
                return ignoreConstraints ? TypeEntryCache.Of<object>() : type.BaseType;

            return GetBaseType(type) ?? TypeEntryCache.Of<object>();
        }

        /// <summary>
        /// Reads the attributes of a generic parameter, tolerating unfinished builders.
        /// </summary>
        private static GenericParameterAttributes GetGenericParameterAttributes(TypeEntry type)
        {
            return type.GenericParameterAttributes;
        }

        /// <summary>
        /// Checks if a lambda signature matches a delegate.
        /// </summary>
        private static int LambdaDistance(TypeResolutionContext ctx, TypeEntry varType, TypeEntry exprType)
        {
            var varWrapper = ReflectionHelper.WrapDelegate(ctx, varType);
            var exprWrapper = ReflectionHelper.WrapDelegate(ctx, exprType);

            if (varWrapper.ArgumentTypes.Length != exprWrapper.ArgumentTypes.Length)
                return int.MaxValue;

            var sum = 0;
            for (var idx = 0; idx < varWrapper.ArgumentTypes.Length; idx++)
            {
                var currVar = varWrapper.ArgumentTypes[idx];
                var currExpr = exprWrapper.ArgumentTypes[idx];

                var dist = currVar.DistanceFrom(ctx, currExpr);
                if (dist == int.MaxValue)
                    return int.MaxValue;

                sum += dist;
            }

            var returnDist = LambdaReturnDistance(ctx, varWrapper.ReturnType, exprWrapper.ReturnType);
            if (returnDist == int.MaxValue)
                return int.MaxValue;

            return sum + returnDist;
        }

        /// <summary>
        /// How far a lambda literal's result is from what a delegate returns.
        ///
        /// A literal written with its argument types knows what its body produces, and two overloads
        /// may differ in nothing else - the selector of Sum is a Func to int, to long, to double and
        /// to decimal, and which of them is meant is a question only the body can answer.
        ///
        /// Two answers are free: a literal written without argument types cannot have bound its body
        /// yet and says UnspecifiedType, and a parameter returning a type argument is not a
        /// constraint but the thing inference is about to work out.
        ///
        /// What is not free is a result that merely converts. A lambda is compiled into a method of
        /// exactly one signature, so a body returning int does not make a Func&lt;object&gt; - there
        /// is no boxing step to insert between the two - and matching it leniently here would accept
        /// a call that emission cannot produce.
        /// </summary>
        private static int LambdaReturnDistance(TypeResolutionContext ctx, TypeEntry wanted, TypeEntry given)
        {
            if (ReferenceEquals(wanted, null) || ReferenceEquals(given, null))
                return 0;

            if (given.Is<UnspecifiedType>() || MentionsGenericParameter(wanted))
                return 0;

            return wanted.DistanceFrom(ctx, given, true);
        }

        /// <summary>
        /// Whether a type is, or is built out of, a type argument that has yet to be worked out.
        /// </summary>
        private static bool MentionsGenericParameter(TypeEntry type)
        {
            if (ReferenceEquals(type, null))
                return false;

            if (type.IsGenericParameter)
                return true;

            if (MentionsGenericParameter(type.ElementType))
                return true;

            foreach (var curr in type.GenericArguments)
                if (MentionsGenericParameter(curr))
                    return true;

            return false;
        }

        #endregion

        #region Most common type

        /// <summary>
        /// Gets the most common type that all the given types would fit into.
        /// </summary>
        public static TypeEntry GetMostCommonType(this TypeEntry[] types, TypeResolutionContext ctx)
        {
            if (types.Length == 0)
                return null;

            if (types.Length == 1)
                return types[0];

            // try to get the most wide type
            TypeEntry curr = null;
            foreach (var type in types)
            {
                if (type.IsVoid())
                    throw new LensCompilerException(CompilerMessages.NoCommonType);

                curr = GetMostCommonType(ctx, curr, type);
                if (curr.Is<object>())
                    break;
            }

            // check for cases that are not transitively castable
            // for example: new [1; 1.2; null]
            // int -> double is fine, double -> Nullable<double> is fine as well
            // but int -> Nullable<double> is currently forbidden
            foreach (var type in types)
            {
                if (!curr.IsExtendablyAssignableFrom(ctx, type))
                {
                    curr = TypeEntryCache.Of<object>();
                    break;
                }
            }

            if (!curr.IsAnyOf(TypeEntryCache.Of<object>(), TypeEntryCache.Of<ValueType>(), TypeEntryCache.Of<Delegate>(), TypeEntryCache.Of<Enum>()))
                return curr;

            // try to get common interfaces
            var ifaces = types[0].GetInterfaces(ctx).AsEnumerable().ToList();
            for (var idx = 1; idx < types.Length; idx++)
            {
                var newIfaces = types[idx].IsInterface ? new[] {types[idx]} : types[idx].GetInterfaces(ctx);
                ifaces = ifaces.Intersect(newIfaces).ToList();
                if (!ifaces.Any())
                    break;
            }

            var iface = GetMostSpecificInterface(ctx, ifaces);
            return iface ?? TypeEntryCache.Of<object>();
        }

        /// <summary>
        /// Gets the most common type between two.
        /// </summary>
        private static TypeEntry GetMostCommonType(TypeResolutionContext ctx, TypeEntry left, TypeEntry right)
        {
            // corner case
            if (left == null || left == right)
                return right;

            if (right.IsInterface)
                return TypeEntryCache.Of<object>();

            // valuetype & null
            if (left.Is<NullType>() && right.IsValueType)
                return TypeEntryCache.Of(typeof(Nullable<>)).MakeGeneric(ctx, new[] {right});

            if (right.Is<NullType>() && left.IsValueType)
                return TypeEntryCache.Of(typeof(Nullable<>)).MakeGeneric(ctx, new[] {left});

            // valuetype & Nullable<valuetype>
            if (left.IsNullableType() && left.GenericArguments[0] == right)
                return left;

            if (right.IsNullableType() && right.GenericArguments[0] == left)
                return right;

            // numeric extensions
            if (left.IsNumericType() && right.IsNumericType())
                return GetNumericOperationType(left, right) ?? TypeEntryCache.Of<object>();

            // arrays
            if (left.IsArray && right.IsArray)
            {
                var leftElem = left.ElementType;
                var rightElem = right.ElementType;
                return leftElem.IsValueType || rightElem.IsValueType
                    ? TypeEntryCache.Of<object>()
                    : GetMostCommonType(ctx, leftElem, rightElem).MakeArray(ctx);
            }

            // inheritance
            var currLeft = left;
            while (currLeft != null)
            {
                var currRight = right;
                while (currRight != null)
                {
                    if (currLeft == currRight)
                        return currLeft;

                    currRight = currRight.BaseType;
                }

                currLeft = currLeft.BaseType;
            }

            return TypeEntryCache.Of<object>();
        }

        /// <summary>
        /// Finds the most specific interface from that contains all others.
        /// </summary>
        private static TypeEntry GetMostSpecificInterface(TypeResolutionContext ctx, IEnumerable<TypeEntry> ifaces)
        {
            var remaining = ifaces.ToDictionary(i => i, i => true);
            foreach (var iface in ifaces)
            {
                foreach (var curr in iface.GetInterfaces(ctx))
                    remaining.Remove(curr);
            }

            if (remaining.Count == 1)
                return remaining.First().Key;

            var preferred = new[] {TypeEntryCache.Of(typeof(IList<>)), TypeEntryCache.Of(typeof(IEnumerable<>)), TypeEntryCache.Of<IList>()};
            foreach (var pref in preferred)
            {
                foreach (var curr in remaining.Keys)
                    if (curr == pref || (curr.IsGenericType && curr.GetGenericDefinition() == pref))
                        return curr;
            }

            return null;
        }

        #endregion

        #region Numeric type conversions

        /// <summary>
        /// Get the best numeric operation type for two operands.
        /// </summary>
        /// <param name="type1">First operand type.</param>
        /// <param name="type2">Second operand type.</param>
        /// <returns>Operation type. <c>null</c> if operation not permitted.</returns>
        public static TypeEntry GetNumericOperationType(TypeEntry type1, TypeEntry type2)
        {
            if (type1.IsFloatType() || type2.IsFloatType())
            {
                if (type1.Is<long>() || type2.Is<long>())
                    return TypeEntryCache.Of<double>();

                return WidestNumericType(FloatTypes, type1, type2);
            }

            if (type1.IsSignedIntegerType() && type2.IsSignedIntegerType())
            {
                var types = SignedIntegerTypes.SkipWhile(type => !type.Is<int>()).ToArray();
                return WidestNumericType(types, type1, type2);
            }

            if (type1.IsUnsignedIntegerType() && type2.IsUnsignedIntegerType())
            {
                var index1 = Array.IndexOf(UnsignedIntegerTypes, type1);
                var index2 = Array.IndexOf(UnsignedIntegerTypes, type2);
                var uintIndex = Array.IndexOf(UnsignedIntegerTypes, TypeEntryCache.Of<uint>());
                if (index1 < uintIndex && index2 < uintIndex)
                    return TypeEntryCache.Of<int>();

                return WidestNumericType(UnsignedIntegerTypes, type1, type2);
            }

            // type1.IsSignedIntegerType() && type2.IsUnsignedIntegerType() or vice versa:
            return null;
        }

        private static TypeEntry WidestNumericType(TypeEntry[] types, TypeEntry type1, TypeEntry type2)
        {
            var index1 = Array.IndexOf(types, type1);
            var index2 = Array.IndexOf(types, type2);
            var index = Math.Max(index1, index2);
            return types[index < 0 ? 0 : index];
        }

        private static int NumericTypeConversion(TypeEntry varType, TypeEntry exprType)
        {
            if (varType.IsSignedIntegerType() && exprType.IsSignedIntegerType())
                return SimpleNumericConversion(varType, exprType, SignedIntegerTypes);

            if (varType.IsUnsignedIntegerType() && exprType.IsUnsignedIntegerType())
                return SimpleNumericConversion(varType, exprType, UnsignedIntegerTypes);

            if (varType.IsFloatType() && exprType.IsFloatType())
                return SimpleNumericConversion(varType, exprType, FloatTypes);

            if (varType.IsSignedIntegerType() && exprType.IsUnsignedIntegerType())
                return UnsignedToSignedConversion(varType, exprType);

            if (varType.IsFloatType() && exprType.IsSignedIntegerType())
                return SignedToFloatConversion(varType, exprType);

            if (varType.IsFloatType() && exprType.IsUnsignedIntegerType())
                return UnsignedToFloatConversion(varType, exprType);

            return int.MaxValue;
        }

        private static int SimpleNumericConversion(TypeEntry varType, TypeEntry exprType, TypeEntry[] conversionChain)
        {
            var varTypeIndex = Array.IndexOf(conversionChain, varType);
            var exprTypeIndex = Array.IndexOf(conversionChain, exprType);
            if (varTypeIndex < exprTypeIndex)
                return int.MaxValue;

            return varTypeIndex - exprTypeIndex;
        }

        private static int UnsignedToSignedConversion(TypeEntry varType, TypeEntry exprType)
        {
            // no unsigned type can be converted to the signed byte.
            if (varType.Is<sbyte>())
                return int.MaxValue;

            var index = Array.IndexOf(SignedIntegerTypes, varType);
            var correspondingUnsignedType = UnsignedIntegerTypes[index - 1]; // only expanding conversions allowed

            var result = SimpleNumericConversion(correspondingUnsignedType, exprType, UnsignedIntegerTypes);
            return result == int.MaxValue
                ? int.MaxValue
                : result + 1;
        }

        private static int SignedToFloatConversion(TypeEntry varType, TypeEntry exprType)
        {
            var targetType = GetCorrespondingSignedType(varType);

            var result = SimpleNumericConversion(targetType, exprType, SignedIntegerTypes);
            return result == int.MaxValue
                ? int.MaxValue
                : result + 1;
        }

        private static int UnsignedToFloatConversion(TypeEntry varType, TypeEntry exprType)
        {
            if (exprType.Is<ulong>() && varType.Is<decimal>())
            {
                // ulong can be implicitly converted only to decimal.
                return 1;
            }
            else
            {
                // If type is not ulong we need to convert it to the corresponding signed type.
                var correspondingSignedType = GetCorrespondingSignedType(varType);
                var result = UnsignedToSignedConversion(correspondingSignedType, exprType);

                return result == int.MaxValue
                    ? int.MaxValue
                    : result + 1;
            }
        }

        private static TypeEntry GetCorrespondingSignedType(TypeEntry floatType)
        {
            if (floatType.Is<float>())
                return TypeEntryCache.Of<int>();

            if (floatType.Is<double>() || floatType.Is<decimal>())
                return TypeEntryCache.Of<long>();

            return null;
        }

        #endregion

        #region Type list distance

        /// <summary>
        /// Gets total distance between two sets of argument types.
        /// </summary>
        public static MethodLookupResult<T> ArgumentDistance<T>(TypeResolutionContext ctx, IEnumerable<TypeEntry> passedTypes, TypeEntry[] actualTypes, T method, bool isVariadic)
        {
            if (!isVariadic)
                return new MethodLookupResult<T>(method, TypeListDistance(ctx, passedTypes, actualTypes), actualTypes);

            var simpleCount = actualTypes.Length - 1;
            var variadicArgs = passedTypes.Skip(simpleCount).ToArray();

            var simpleDistance = TypeListDistance(ctx, passedTypes.Take(simpleCount), actualTypes.Take(simpleCount));
            var variadicDistance = VariadicArgumentDistance(ctx, variadicArgs, actualTypes[simpleCount]);
            var distance = simpleDistance == int.MaxValue || variadicDistance == int.MaxValue ? int.MaxValue : simpleDistance + variadicDistance;

            // the array is passed as it is rather than assembled from the remaining arguments: the
            // call is in normal form, and nothing about it is variadic any more
            var isExpanded = !(variadicArgs.Length == 1 && variadicArgs[0] == actualTypes[simpleCount]);

            return new MethodLookupResult<T>(method, distance, actualTypes, isExpanded);
        }

        /// <summary>
        /// Gets total distance between two sequence of types.
        /// </summary>
        public static int TypeListDistance(TypeResolutionContext ctx, IEnumerable<TypeEntry> passedArgs, IEnumerable<TypeEntry> calleeArgs)
        {
            var passedIter = passedArgs.GetEnumerator();
            var calleeIter = calleeArgs.GetEnumerator();

            var totalDist = 0;
            while (true)
            {
                var passedOk = passedIter.MoveNext();
                var calleeOk = calleeIter.MoveNext();

                // argument count differs: method cannot be applied
                if (passedOk != calleeOk)
                    return int.MaxValue;

                // both sequences have finished
                if (!calleeOk)
                    return totalDist;

                var dist = calleeIter.Current.DistanceFrom(ctx, passedIter.Current);
                if (dist == int.MaxValue)
                    return int.MaxValue;

                totalDist += dist;
            }
        }

        /// <summary>
        /// Counts the parameters that ask for an expression tree and are being handed a lambda.
        ///
        /// This is the tie-break between an Enumerable overload and the Queryable one that differs
        /// from it only in that parameter. Both are equally near by argument distance - a lambda
        /// reaches Func&lt;T,bool&gt; and Expression&lt;Func&lt;T,bool&gt;&gt; alike, and the receiver
        /// is one step from IEnumerable&lt;T&gt; and from IQueryable&lt;T&gt; alike - so nothing in the
        /// distance model separates them, and leaving it at that would report an ambiguity for the
        /// most ordinary LINQ call there is.
        ///
        /// C# arrives at the same answer from IQueryable&lt;T&gt; being the better conversion target;
        /// the preference is spelled out here instead, because a query the author meant to run in
        /// the database silently running in memory is the worse of the two failures.
        /// </summary>
        public static int ExpressionTreeAffinity(IEnumerable<TypeEntry> passedArgs, IEnumerable<TypeEntry> calleeArgs)
        {
            var passed = passedArgs.ToArray();
            var callee = calleeArgs.ToArray();

            var result = 0;
            var count = Math.Min(passed.Length, callee.Length);
            for (var idx = 0; idx < count; idx++)
            {
                if (!callee[idx].IsExpressionType() || ReferenceEquals(passed[idx], null))
                    continue;

                if (passed[idx].IsLambdaType() || passed[idx].IsCallableType())
                    result++;
            }

            return result;
        }

        /// <summary>
        /// Narrows a list of applicable candidates down to the best ones: nearest first, then the
        /// ones that want an expression tree, then the ones that take the arguments as they are
        /// rather than packing them, then the ones nothing is more specific than.
        ///
        /// More than one survivor means the call really is ambiguous.
        /// </summary>
        public static T[] BestCandidates<T>(TypeResolutionContext ctx, TypeEntry[] argTypes, T[] applicable, Func<T, int> distanceGetter, Func<T, TypeEntry[]> argsGetter, Func<T, bool> isExpandedGetter = null)
        {
            var min = applicable.Min(distanceGetter);
            var best = applicable.Where(x => distanceGetter(x) == min).ToArray();
            if (best.Length < 2)
                return best;

            var maxAffinity = best.Max(x => ExpressionTreeAffinity(argTypes, argsGetter(x)));
            best = best.Where(x => ExpressionTreeAffinity(argTypes, argsGetter(x)) == maxAffinity).ToArray();
            if (best.Length < 2)
                return best;

            // a candidate whose param array is being assembled out of the arguments loses to one that
            // takes them as they are. Without this, 'string::Join ", " someSequence' is as near to
            // Join(string, params object[]) - one step to object, one for the packing - as it is to
            // Join<T>(string, IEnumerable<T>), and the sequence would be joined as a single item.
            //
            // C# has the same rule: an applicable member in normal form beats one that is only
            // applicable in expanded form.
            if (isExpandedGetter != null)
            {
                var normal = best.Where(x => !isExpandedGetter(x)).ToArray();
                if (normal.Length > 0)
                    best = normal;

                if (best.Length < 2)
                    return best;
            }

            return MostSpecific(ctx, best, argsGetter);
        }

        /// <summary>
        /// Reduces a set of equally near candidates to the ones no other candidate is strictly more
        /// specific than.
        ///
        /// A signature is more specific than another when everything it accepts the other accepts
        /// too, and not the other way round. This is what decides between Queryable's Count, which
        /// takes IQueryable&lt;T&gt;, and Enumerable's, which takes IEnumerable&lt;T&gt;: both are
        /// exactly as near to an IQueryable receiver, but only one of them is the reason the other
        /// applies at all.
        ///
        /// C# reaches the same answer through its better-conversion-target rule.
        /// </summary>
        public static T[] MostSpecific<T>(TypeResolutionContext ctx, T[] candidates, Func<T, TypeEntry[]> argsGetter)
        {
            if (candidates.Length < 2)
                return candidates;

            var result = new List<T>();
            foreach (var candidate in candidates)
            {
                var beaten = candidates.Any(other => !ReferenceEquals(other, candidate) && IsMoreSpecific(ctx, argsGetter(other), argsGetter(candidate)));
                if (!beaten)
                    result.Add(candidate);
            }

            return result.Count == 0 ? candidates : result.ToArray();
        }

        /// <summary>
        /// Checks whether one signature accepts strictly less than another does.
        /// </summary>
        private static bool IsMoreSpecific(TypeResolutionContext ctx, TypeEntry[] left, TypeEntry[] right)
        {
            if (left.Length != right.Length)
                return false;

            var better = false;
            for (var idx = 0; idx < left.Length; idx++)
            {
                if (left[idx] == right[idx])
                    continue;

                var leftFitsRight = right[idx].IsExtendablyAssignableFrom(ctx, left[idx]);
                var rightFitsLeft = left[idx].IsExtendablyAssignableFrom(ctx, right[idx]);

                if (leftFitsRight && !rightFitsLeft)
                    better = true;
                else if (rightFitsLeft && !leftFitsRight)
                    return false;
            }

            return better;
        }

        /// <summary>
        /// Calculates the compound distance of a list of arguments packed into a param array.
        /// </summary>
        private static int VariadicArgumentDistance(TypeResolutionContext ctx, IEnumerable<TypeEntry> passedArgs, TypeEntry variadicArg)
        {
            var args = passedArgs.ToArray();

            // variadic function invoked with an array: no conversion
            if (args.Length == 1 && args[0] == variadicArg)
                return 0;

            var sum = 0;
            var elemType = variadicArg.ElementType;

            foreach (var curr in args)
            {
                var currDist = elemType.DistanceFrom(ctx, curr);
                if (currDist == int.MaxValue)
                    return int.MaxValue;

                sum += currDist;
            }

            // 1 extra distance point for packing arguments into the array:
            // otherwise fun(int) and fun(int, object[]) will have equal distance for `fun 1` and cause an ambiguity error
            return sum + 1;
        }

        #endregion

        #region Interface implementations and generic type applications

        /// <summary>
        /// Checks if a type implements an interface.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <param name="iface">Desired interface.</param>
        /// <param name="unwindGenerics">A flag indicating that generic arguments should be discarded from both the type and the interface.</param>
        public static bool Implements(this TypeEntry type, TypeResolutionContext ctx, TypeEntry iface, bool unwindGenerics)
        {
            var ifaces = type.GetInterfaces(ctx);
            if (type.IsInterface)
                ifaces = ifaces.Union(new[] {type}).ToArray();

            if (unwindGenerics)
            {
                for (var idx = 0; idx < ifaces.Length; idx++)
                {
                    var curr = ifaces[idx];
                    if (curr.IsGenericType)
                        ifaces[idx] = curr.GetGenericDefinition();
                }

                if (iface.IsGenericType)
                    iface = iface.GetGenericDefinition();
            }

            return ifaces.Contains(iface);
        }

        /// <summary>
        /// Finds an implementation of a generic interface.
        /// </summary>
        /// <param name="type">Type to find the implementation in.</param>
        /// <param name="iface">Desirrable interface.</param>
        /// <returns>Implementation of the generic interface or null if none.</returns>
        public static TypeEntry ResolveImplementationOf(this TypeEntry type, TypeResolutionContext ctx, TypeEntry iface)
        {
            if (iface.IsGenericType && !iface.IsGenericTypeDefinition)
                iface = iface.GenericDefinition;

            var ifaces = type.GetInterfaces(ctx);
            if (type.IsInterface)
                ifaces = ifaces.Union(new[] {type}).ToArray();

            return ifaces.FirstOrDefault(
                x => x == iface || (x.IsGenericType && x.GetGenericDefinition() == iface)
            );
        }

        /// <summary>
        /// Resolves the common implementation of the given interface for two types.
        /// </summary>
        /// <param name="iface">Interface to find an implementation for in given types.</param>
        /// <param name="type1">First type to examine.</param>
        /// <param name="type2">First type to examine.</param>
        /// <returns>Common implementation of an interface, or null if none.</returns>
        public static TypeEntry ResolveCommonImplementationFor(this TypeEntry iface, TypeResolutionContext ctx, TypeEntry type1, TypeEntry type2)
        {
            var impl1 = type1.ResolveImplementationOf(ctx, iface);
            var impl2 = type2.ResolveImplementationOf(ctx, iface);
            return impl1 == impl2 ? impl1 : null;
        }

        /// <summary>
        /// Checks if a type is (or implements) a specified type with any generic argument values given.
        /// Example: Dictionary&lt;A, B&gt; is Dictionary`2
        /// </summary>
        /// <param name="type">Closed type to test.</param>
        /// <param name="genericType">Generic type.</param>
        public static bool IsAppliedVersionOf(this TypeEntry type, TypeResolutionContext ctx, TypeEntry genericType)
        {
            if (type.IsInterface && !genericType.IsInterface)
                throw new ArgumentException(string.Format("Interface {0} cannot implement a type! ({1} given).", type.FullName, genericType.FullName));

            if (!type.IsGenericType || !genericType.IsGenericType)
                return false;

            return genericType.IsInterface
                ? type.Implements(ctx, genericType, true)
                : type.GetGenericDefinition() == genericType.GetGenericDefinition();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the generic definition of a generic type, exactly as
        /// <see cref="Type.GetGenericTypeDefinition"/> does: a definition is its own definition,
        /// while <see cref="TypeEntry.GenericDefinition"/> reports null for one.
        ///
        /// Only meaningful for a type that is generic, which is what the reflection call requires too.
        /// </summary>

        #endregion
    }
}
