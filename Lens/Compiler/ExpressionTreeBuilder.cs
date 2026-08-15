using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Locals;
using Lens.SyntaxTree.Expressions;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.Expressions.Instantiation;
using Lens.SyntaxTree.Literals;
using Lens.SyntaxTree.Operators;
using Lens.SyntaxTree.Operators.Binary;
using Lens.SyntaxTree.Operators.TypeBased;
using Lens.SyntaxTree.Operators.Unary;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// The second backend for a lambda body: instead of compiling it to IL, walks the bound tree and
    /// emits the calls into <see cref="Expression"/> that build the equivalent tree at runtime.
    ///
    /// Only the subset a query provider can actually translate is supported. Everything else is
    /// rejected here, by name and with a location, because a compile error the author can act on
    /// beats a tree that builds and then fails inside the provider.
    /// </summary>
    internal class ExpressionTreeBuilder
    {
        #region Constructor

        public ExpressionTreeBuilder(Context ctx, LambdaNode lambda, TypeEntry treeType)
        {
            _ctx = ctx;
            _lambda = lambda;
            _treeType = treeType;
            _parameters = new Dictionary<string, LocalBuilder>();
        }

        #endregion

        #region Fields

        private readonly Context _ctx;
        private readonly LambdaNode _lambda;

        /// <summary>
        /// The Expression&lt;TDelegate&gt; the tree must come out as.
        /// </summary>
        private readonly TypeEntry _treeType;

        /// <summary>
        /// The IL local that holds the ParameterExpression of each of the lambda's arguments.
        /// </summary>
        private readonly Dictionary<string, LocalBuilder> _parameters;

        private ILGenerator Gen => _ctx.CurrentMethod.Generator;

        #endregion

        #region Entry point

        /// <summary>
        /// Emits the code that builds the tree and leaves it on the stack.
        /// </summary>
        public void Emit()
        {
            var delegateType = _treeType.GenericArguments[0].Materialize();

            // the delegate may be instantiated over a type the script declared, and reflection
            // cannot enumerate the members of such an instantiation - the wrapper knows the way
            var invoke = ReflectionHelper.WrapDelegate(_ctx.Resolver, delegateType);
            var expectedArgs = invoke.ArgumentTypes;
            var expectedReturn = invoke.ReturnType.Materialize();

            var args = _lambda.Arguments;
            if (args.Count != expectedArgs.Length)
                Error(_lambda, CompilerMessages.LambdaArgumentsCountMismatch, expectedArgs.Length, args.Count);

            // ParameterExpression[] parameters = new ParameterExpression[n]
            var array = DeclareTemp(typeof(ParameterExpression[]));
            Gen.EmitConstant(args.Count);
            Gen.EmitCreateArray(typeof(ParameterExpression));
            Gen.EmitSaveLocal(array);

            for (var idx = 0; idx < args.Count; idx++)
            {
                var local = DeclareTemp(typeof(ParameterExpression));
                _parameters[args[idx].Name] = local;

                PushType(expectedArgs[idx].Materialize());
                Gen.EmitConstant(args[idx].Name);
                Gen.EmitCall(FactoryParameter);
                Gen.EmitSaveLocal(local);

                Gen.EmitLoadLocal(array);
                Gen.EmitConstant(idx);
                Gen.EmitLoadLocal(local);
                Gen.Emit(OpCodes.Stelem_Ref);
            }

            // the body, converted to whatever the delegate promised to return
            var body = _lambda.GetExpressionTreeBody();
            var bodyType = Translate(body);
            Convert(bodyType, expectedReturn);

            Gen.EmitLoadLocal(array);
            Gen.EmitCall(FactoryLambda.MakeGenericMethod(delegateType));
        }

        #endregion

        #region Dispatch

        /// <summary>
        /// Emits the code that builds the tree for one node and leaves it on the stack.
        /// </summary>
        /// <returns>The type of the value the emitted Expression computes.</returns>
        private Type Translate(NodeBase node)
        {
            switch (node)
            {
                case null:
                    throw new InvalidOperationException("An expression tree node is missing!");

                // a block with a single expression is the line form of a lambda body; anything
                // longer has no counterpart in the Expression API worth building
                case CodeBlockNode block:
                    return Translate(SingleStatementOf(block));

                case NullNode _:
                    return TranslateNull(typeof(object));

                case NodeBase constant when constant.IsConstant:
                    return TranslateConstant(constant);

                case GetIdentifierNode identifier:
                    return TranslateIdentifier(identifier);

                case GetMemberNode member:
                    return TranslateMember(member);

                case GetIndexNode index:
                    return TranslateIndex(index);

                case NullSafeChainNode _:
                    Error(node, CompilerMessages.ExpressionTreeNullSafe);
                    return null;

                case NewObjectNode creation:
                    return TranslateNewObject(creation);

                case NewArrayNode array:
                    return TranslateNewArray(array);

                case InvocationNode invocation:
                    return TranslateInvocation(invocation);

                case CastOperatorNode cast:
                    return TranslateCast(cast);

                case IsOperatorNode check:
                    return TranslateTypeCheck(check);

                case DefaultOperatorNode fallback:
                    return TranslateDefault(fallback);

                case BooleanOperatorNode boolean:
                    return TranslateBoolean(boolean);

                case CoalesceOperatorNode coalesce:
                    return TranslateCoalesce(coalesce);

                case BinaryOperatorNodeBase binary:
                    return TranslateBinary(binary);

                case UnaryOperatorNodeBase unary:
                    return TranslateUnary(unary);

                case IfNode conditional:
                    return TranslateCondition(conditional);

                default:
                    return TranslateExpansion(node);
            }
        }

        /// <summary>
        /// Translates whatever binding rewrote a node into, when the node itself has no direct
        /// counterpart. This is what turns string concatenation - which LENS expands into a call to
        /// string.Concat - into the very MethodCallExpression C# would have built.
        /// </summary>
        private Type TranslateExpansion(NodeBase node)
        {
            var expansion = _ctx.Expanded(node);
            if (ReferenceEquals(expansion, node))
            {
                Error(node, CompilerMessages.ExpressionTreeUnsupportedNode, Describe(node));
                return null;
            }

            return Translate(expansion);
        }

        #endregion

        #region Leaves

        /// <summary>
        /// Emits Expression.Constant of a value the enclosing method computes.
        ///
        /// A literal and a folded constant come out as C#'s would. Anything else that is closed over
        /// the lambda's parameters - a global property, an unrolled 'let' - is evaluated once, where
        /// the lambda is created, which is the only moment its value is reachable at all.
        /// </summary>
        private Type TranslateConstant(NodeBase node)
        {
            var type = node.Resolve(_ctx).Materialize();
            if (type == typeof(NullType))
                return TranslateNull(typeof(object));

            node.Emit(_ctx, true);
            Box(type);
            PushType(type);
            Gen.EmitCall(FactoryConstant);

            return type;
        }

        private Type TranslateNull(Type type)
        {
            Gen.EmitNull();
            PushType(type);
            Gen.EmitCall(FactoryConstant);

            return type;
        }

        /// <summary>
        /// A name is one of three things: an argument of the lambda, which is the tree's own
        /// parameter; a local of the enclosing method that the lambda captured, which is a field of
        /// the closure and therefore still reads its current value when the tree runs; or something
        /// with no place in the tree at all, whose value is baked in as a constant.
        /// </summary>
        private Type TranslateIdentifier(GetIdentifierNode node)
        {
            if (node.Identifier != null && _parameters.TryGetValue(node.Identifier, out var parameter))
            {
                Gen.EmitLoadLocal(parameter);
                return node.Resolve(_ctx).Materialize();
            }

            var local = node.Local ?? _ctx.Scope.FindLocal(node.Identifier);
            if (local != null && local.IsClosured)
                return TranslateClosuredLocal(node, local);

            // a global property, or a local the lambda does not actually capture: its value is what
            // the tree can carry, not the name
            return TranslateConstantOfValue(node);
        }

        /// <summary>
        /// Emits Expression.Field over the closure instance, which is how C# refers to a captured
        /// variable: the tree reads the field when it runs, so a later assignment is visible to a
        /// query that has not been enumerated yet.
        /// </summary>
        private Type TranslateClosuredLocal(NodeBase node, Local local)
        {
            var closureType = _ctx.Scope.EmitClosureInstance(_ctx, local);
            var field = _ctx.ResolveField(closureType, local.ClosureFieldName);

            Box(closureType.Materialize());
            PushType(closureType.Materialize());
            Gen.EmitCall(FactoryConstant);

            PushField(field.FieldInfo);
            Gen.EmitCall(FactoryField);

            return field.FieldType.Materialize();
        }

        /// <summary>
        /// Evaluates a subexpression in the enclosing method and wraps the result in a constant.
        /// </summary>
        private Type TranslateConstantOfValue(NodeBase node)
        {
            var type = node.Resolve(_ctx).Materialize();

            node.Emit(_ctx, true);
            Box(type);
            PushType(type);
            Gen.EmitCall(FactoryConstant);

            return type;
        }

        #endregion

        #region Member and element access

        private Type TranslateMember(GetMemberNode node)
        {
            var declaring = node.BoundType;

            // arr.Length is not a member at all in the tree model
            if (declaring != null && declaring.IsArray && node.MemberName == "Length")
            {
                Translate(node.Expression);
                Gen.EmitCall(FactoryArrayLength);
                return typeof(int);
            }

            if (node.BoundMethod != null)
            {
                Error(node, CompilerMessages.ExpressionTreeUnsupportedNode, "method as a value");
                return null;
            }

            PushInstance(node.IsStaticAccess ? null : node.Expression);

            if (node.BoundField != null)
            {
                PushField(node.BoundField.FieldInfo);
                Gen.EmitCall(FactoryField);
                return node.BoundField.FieldType.Materialize();
            }

            PushMethod(node.BoundProperty.Getter);
            Gen.EmitCall(FactoryProperty);
            return node.BoundProperty.PropertyType.Materialize();
        }

        private Type TranslateIndex(GetIndexNode node)
        {
            var getter = node.BoundGetter;
            if (getter == null)
            {
                Translate(node.Expression);
                var indexType = Translate(node.Index);
                Convert(indexType, typeof(int));
                Gen.EmitCall(FactoryArrayIndex);

                return node.Expression.Resolve(_ctx).ElementType.Materialize();
            }

            // an indexer reads as a call to its getter, which is exactly the shape C# builds
            Translate(node.Expression);
            PushMethod(getter.MethodInfo);
            EmitArgumentArray(new[] {node.Index}, getter.ArgumentTypes.Select(x => x.Materialize()).ToArray());
            Gen.EmitCall(FactoryCallInstance);

            return getter.ReturnType.Materialize();
        }

        #endregion

        #region Calls and creation

        private Type TranslateInvocation(InvocationNode node)
        {
            var method = (MethodWrapper) node.BoundCallable(_ctx);
            var source = node.BoundInvocationSource(_ctx);
            var arguments = node.BoundArguments(_ctx);
            var argTypes = method.ArgumentTypes.Select(x => x.Materialize()).ToArray();

            // a call on a delegate value has no MethodInfo of its own in the tree model, and a
            // provider could not translate one anyway
            if (source != null && source.Resolve(_ctx).IsCallableType())
            {
                Error(node, CompilerMessages.ExpressionTreeUnsupportedNode, "delegate invocation");
                return null;
            }

            var isStatic = source == null;
            if (!isStatic)
                Translate(source);

            PushMethod(method.MethodInfo);
            EmitArgumentArray(RealArguments(arguments, argTypes.Length), argTypes);
            Gen.EmitCall(isStatic ? FactoryCallStatic : FactoryCallInstance);

            return method.ReturnType.Materialize();
        }

        private Type TranslateNewObject(NewObjectNode node)
        {
            var ctor = node.BoundConstructor(_ctx);
            if (ctor == null)
                return TranslateExpansion(node);

            var argTypes = ctor.ArgumentTypes.Select(x => x.Materialize()).ToArray();

            PushConstructor(ctor.ConstructorInfo);
            EmitArgumentArray(RealArguments(node.BoundArguments(_ctx), argTypes.Length), argTypes);
            Gen.EmitCall(FactoryNew);

            return ctor.DeclaringType.Materialize();
        }

        private Type TranslateNewArray(NewArrayNode node)
        {
            var type = node.Resolve(_ctx);
            var elementType = type.ElementType.Materialize();

            PushType(elementType);
            EmitArgumentArray(node.Expressions, node.Expressions.Select(_ => elementType).ToArray());
            Gen.EmitCall(FactoryNewArrayInit);

            return type.Materialize();
        }

        #endregion

        #region Operators

        private Type TranslateBoolean(BooleanOperatorNode node)
        {
            var left = Translate(node.LeftOperand);
            Convert(left, typeof(bool));
            var right = Translate(node.RightOperand);
            Convert(right, typeof(bool));

            Gen.EmitCall(node.Kind == LogicalOperatorKind.And ? FactoryAndAlso : FactoryOrElse);
            return typeof(bool);
        }

        private Type TranslateCoalesce(CoalesceOperatorNode node)
        {
            var type = node.Resolve(_ctx);
            var leftType = node.LeftOperand.Resolve(_ctx);

            // Coalesce works out the result type itself, from the left operand with its nullability
            // stripped - so anything the two operands do not already agree on cannot be expressed
            var leftBase = leftType.GetNullableUnderlyingType() ?? leftType;
            if (leftBase != type && leftBase != (type.GetNullableUnderlyingType() ?? type))
                Error(node, CompilerMessages.ExpressionTreeUnsupportedOperator, node.Representation);

            Translate(node.LeftOperand);
            Convert(Translate(node.RightOperand), type.Materialize());
            Gen.EmitCall(FactoryCoalesce);

            return type.Materialize();
        }

        private Type TranslateBinary(BinaryOperatorNodeBase node)
        {
            var overload = node.BoundOperatorMethod;
            var leftType = node.LeftOperand.Resolve(_ctx);
            var rightType = node.RightOperand.Resolve(_ctx);
            var comparison = node as ComparisonOperatorNode;

            if (overload != null)
            {
                var expected = overload.ArgumentTypes.Select(x => x.Materialize()).ToArray();
                Convert(Translate(node.LeftOperand), expected[0]);
                Convert(Translate(node.RightOperand), expected[1]);

                EmitBinaryFactory(node, comparison, true);
                return overload.ReturnType.Materialize();
            }

            // C# builds string concatenation as an Add that names string.Concat, rather than as the
            // plain call LENS's IL backend expands it into
            if (node is AddOperatorNode && node.Resolve(_ctx).Is<string>())
            {
                Convert(Translate(node.LeftOperand), typeof(string));
                Convert(Translate(node.RightOperand), typeof(string));
                PushMethod(StringConcat);
                Gen.EmitCall(FactoryAddMethod);

                return typeof(string);
            }

            // equality against null, and equality of reference types, need no conversion at all -
            // and asking for the common numeric type of a reference type would find none
            if (comparison != null && IsNullComparison(leftType, rightType))
                return TranslateNullComparison(comparison, leftType, rightType);

            if (comparison != null && !leftType.IsNumericType(true) && !leftType.IsNullableType())
            {
                // relations of anything but numbers - string ordering above all - go through a
                // helper in the IL backend that has no counterpart in the tree model
                var isEquality = comparison.Kind == ComparisonOperatorKind.Equals || comparison.Kind == ComparisonOperatorKind.NotEquals;
                var comparable = leftType == rightType && (!leftType.IsValueType || leftType.Is<bool>() || leftType.IsEnum);

                if (!isEquality || !comparable)
                {
                    Error(node, CompilerMessages.ExpressionTreeUnsupportedOperator, node.Representation);
                    return null;
                }

                Translate(node.LeftOperand);
                Translate(node.RightOperand);
                EmitBinaryFactory(node, comparison, false);
                return typeof(bool);
            }

            var operandType = OperandTypeOf(node, leftType, rightType);
            if (operandType == null)
                return TranslateExpansion(node);

            Convert(Translate(node.LeftOperand), operandType);
            Convert(Translate(node.RightOperand), operandType);
            EmitBinaryFactory(node, comparison, false);

            return comparison != null ? typeof(bool) : node.Resolve(_ctx).Materialize();
        }

        /// <summary>
        /// The type both operands have to be brought to, or null when the operator is not one of the
        /// arithmetic ones and has to be taken from its expansion instead.
        /// </summary>
        private Type OperandTypeOf(BinaryOperatorNodeBase node, TypeEntry leftType, TypeEntry rightType)
        {
            if (node is BitOperatorNode || node is XorOperatorNode)
            {
                if (leftType != rightType)
                    return null;

                return leftType.Is<bool>() || leftType.IsIntegerType() || leftType.IsEnum ? leftType.Materialize() : null;
            }

            if (node is PowOperatorNode)
                return typeof(double);

            // a shift keeps the left operand's own type and takes an int on the right, so it does
            // not fit the both-operands-to-one-type shape; LENS also spells delegate composition
            // this way, which has no tree form at all
            if (node is ShiftOperatorNode)
                return null;

            var leftBase = leftType.GetNullableUnderlyingType() ?? leftType;
            var rightBase = rightType.GetNullableUnderlyingType() ?? rightType;

            if (!leftBase.IsNumericType(true) || !rightBase.IsNumericType(true))
                return null;

            var common = Lens.Resolver.TypeExtensions.GetNumericOperationType(leftBase, rightBase);
            if (common == null)
                return null;

            var lifted = leftType.IsNullableType() || rightType.IsNullableType();
            return lifted
                ? typeof(Nullable<>).MakeGenericType(common.Materialize())
                : common.Materialize();
        }

        private bool IsNullComparison(TypeEntry left, TypeEntry right)
        {
            return left.Is<NullType>() || right.Is<NullType>();
        }

        private Type TranslateNullComparison(ComparisonOperatorNode node, TypeEntry leftType, TypeEntry rightType)
        {
            var otherType = leftType.Is<NullType>() ? rightType : leftType;
            var nullType = otherType.IsValueType && !otherType.IsNullableType()
                ? typeof(Nullable<>).MakeGenericType(otherType.Materialize())
                : otherType.Materialize();

            if (leftType.Is<NullType>())
            {
                TranslateNull(nullType);
                Convert(Translate(node.RightOperand), nullType);
            }
            else
            {
                Convert(Translate(node.LeftOperand), nullType);
                TranslateNull(nullType);
            }

            EmitBinaryFactory(node, node, false);
            return typeof(bool);
        }

        private Type TranslateUnary(UnaryOperatorNodeBase node)
        {
            var overload = node.BoundOperatorMethod;
            if (node is InversionOperatorNode)
            {
                Convert(Translate(node.Operand), typeof(bool));
                Gen.EmitCall(FactoryNot);
                return typeof(bool);
            }

            if (node is NegationOperatorNode)
            {
                var type = node.Resolve(_ctx).Materialize();
                if (overload != null)
                {
                    Convert(Translate(node.Operand), overload.ArgumentTypes[0].Materialize());
                    PushMethod(overload.MethodInfo);
                    Gen.EmitCall(FactoryNegateMethod);
                }
                else
                {
                    Convert(Translate(node.Operand), type);
                    Gen.EmitCall(FactoryNegate);
                }

                return type;
            }

            Error(node, CompilerMessages.ExpressionTreeUnsupportedOperator, node.Representation);
            return null;
        }

        private Type TranslateCast(CastOperatorNode node)
        {
            var to = node.Resolve(_ctx).Materialize();
            var from = Translate(node.Expression);

            if (from != to)
            {
                PushType(to);
                Gen.EmitCall(FactoryConvert);
            }

            return to;
        }

        private Type TranslateTypeCheck(IsOperatorNode node)
        {
            Translate(node.Expression);
            PushType(node.Type != null ? node.Type.Materialize() : _ctx.ResolveType(node.TypeSignature).Materialize());
            Gen.EmitCall(FactoryTypeIs);

            return typeof(bool);
        }

        private Type TranslateDefault(DefaultOperatorNode node)
        {
            var type = node.Resolve(_ctx).Materialize();
            PushType(type);
            Gen.EmitCall(FactoryDefault);

            return type;
        }

        private Type TranslateCondition(IfNode node)
        {
            if (node.FalseAction == null)
            {
                Error(node, CompilerMessages.ExpressionTreeUnsupportedNode, "if without else");
                return null;
            }

            var type = node.Resolve(_ctx).Materialize();

            Convert(Translate(node.Condition), typeof(bool));
            Convert(Translate(node.TrueAction), type);
            Convert(Translate(node.FalseAction), type);
            Gen.EmitCall(FactoryCondition);

            return type;
        }

        /// <summary>
        /// Emits the factory call for a binary operator that already has both operands on the stack.
        /// </summary>
        private void EmitBinaryFactory(BinaryOperatorNodeBase node, ComparisonOperatorNode comparison, bool withMethod)
        {
            if (comparison != null)
            {
                // sic! the equality factories take a lifting flag before the method, the relational
                // ones do not, so the two cannot share a call shape
                var isEquality = comparison.Kind == ComparisonOperatorKind.Equals || comparison.Kind == ComparisonOperatorKind.NotEquals;
                if (withMethod)
                {
                    if (isEquality)
                    {
                        Gen.EmitConstant(false);
                        PushMethod(node.BoundOperatorMethod.MethodInfo);
                        Gen.EmitCall(comparison.Kind == ComparisonOperatorKind.Equals ? FactoryEqualLifted : FactoryNotEqualLifted);
                        return;
                    }

                    PushMethod(node.BoundOperatorMethod.MethodInfo);
                    Gen.EmitCall(RelationalFactoryWithMethod(comparison.Kind));
                    return;
                }

                Gen.EmitCall(ComparisonFactory(comparison.Kind));
                return;
            }

            if (withMethod)
            {
                PushMethod(node.BoundOperatorMethod.MethodInfo);
                Gen.EmitCall(ArithmeticFactoryWithMethod(node));
                return;
            }

            Gen.EmitCall(ArithmeticFactory(node));
        }

        private MethodInfo ComparisonFactory(ComparisonOperatorKind kind)
        {
            switch (kind)
            {
                case ComparisonOperatorKind.Equals: return FactoryEqual;
                case ComparisonOperatorKind.NotEquals: return FactoryNotEqual;
                case ComparisonOperatorKind.Less: return FactoryLessThan;
                case ComparisonOperatorKind.LessEquals: return FactoryLessThanOrEqual;
                case ComparisonOperatorKind.Greater: return FactoryGreaterThan;
                default: return FactoryGreaterThanOrEqual;
            }
        }

        private MethodInfo RelationalFactoryWithMethod(ComparisonOperatorKind kind)
        {
            switch (kind)
            {
                case ComparisonOperatorKind.Less: return FactoryLessThanMethod;
                case ComparisonOperatorKind.LessEquals: return FactoryLessThanOrEqualMethod;
                case ComparisonOperatorKind.Greater: return FactoryGreaterThanMethod;
                default: return FactoryGreaterThanOrEqualMethod;
            }
        }

        private MethodInfo ArithmeticFactory(BinaryOperatorNodeBase node)
        {
            switch (node)
            {
                case AddOperatorNode _: return FactoryAdd;
                case SubtractOperatorNode _: return FactorySubtract;
                case MultiplyOperatorNode _: return FactoryMultiply;
                case DivideOperatorNode _: return FactoryDivide;
                case RemainderOperatorNode _: return FactoryModulo;
                case PowOperatorNode _: return FactoryPower;
                case XorOperatorNode _: return FactoryExclusiveOr;
                case BitOperatorNode bit:
                    switch (bit.Kind)
                    {
                        case LogicalOperatorKind.And: return FactoryAnd;
                        case LogicalOperatorKind.Or: return FactoryOr;
                        default: return FactoryExclusiveOr;
                    }

                default:
                    Error(node, CompilerMessages.ExpressionTreeUnsupportedOperator, node.Representation);
                    return null;
            }
        }

        private MethodInfo ArithmeticFactoryWithMethod(BinaryOperatorNodeBase node)
        {
            switch (node)
            {
                case AddOperatorNode _: return FactoryAddMethod;
                case SubtractOperatorNode _: return FactorySubtractMethod;
                case MultiplyOperatorNode _: return FactoryMultiplyMethod;
                case DivideOperatorNode _: return FactoryDivideMethod;
                case RemainderOperatorNode _: return FactoryModuloMethod;
                default:
                    Error(node, CompilerMessages.ExpressionTreeUnsupportedOperator, node.Representation);
                    return null;
            }
        }

        #endregion

        #region Emission helpers

        /// <summary>
        /// Emits Expression.Convert, unless the value already has the type that is wanted.
        /// </summary>
        private void Convert(Type from, Type to)
        {
            if (from == to || to == typeof(void) || to == typeof(UnitType))
                return;

            // an implicit reference conversion needs no node of its own: the factories accept the
            // derived type as it is, and C# puts no Convert there either. Boxing is not one of
            // these, which is why the value types are excluded rather than asked about assignability
            if (!from.IsValueType && !to.IsValueType && TypeEntryCache.Of(to).IsExtendablyAssignableFrom(_ctx.Resolver, TypeEntryCache.Of(from), true))
                return;

            PushType(to);
            Gen.EmitCall(FactoryConvert);
        }

        /// <summary>
        /// Pushes the receiver of a member access, or null for a static one.
        /// </summary>
        private void PushInstance(NodeBase expression)
        {
            if (expression == null)
                Gen.EmitNull();
            else
                Translate(expression);
        }

        /// <summary>
        /// Emits an Expression[] holding the translated arguments, converted to what the callee
        /// declared.
        /// </summary>
        private void EmitArgumentArray(IList<NodeBase> arguments, Type[] expectedTypes)
        {
            var local = DeclareTemp(typeof(Expression[]));

            Gen.EmitConstant(arguments.Count);
            Gen.EmitCreateArray(typeof(Expression));
            Gen.EmitSaveLocal(local);

            for (var idx = 0; idx < arguments.Count; idx++)
            {
                Gen.EmitLoadLocal(local);
                Gen.EmitConstant(idx);

                var actual = Translate(arguments[idx]);
                if (idx < expectedTypes.Length)
                    Convert(actual, expectedTypes[idx]);

                Gen.Emit(OpCodes.Stelem_Ref);
            }

            Gen.EmitLoadLocal(local);
        }

        /// <summary>
        /// Drops the unit pseudoargument a parameterless call carries.
        /// </summary>
        private static IList<NodeBase> RealArguments(IList<NodeBase> arguments, int expectedCount)
        {
            if (expectedCount == 0 && arguments.Count == 1 && arguments[0] is UnitNode)
                return new NodeBase[0];

            return arguments;
        }

        private void Box(Type type)
        {
            if (type.IsValueType || type.IsGenericParameter)
                Gen.EmitBox(type);
        }

        private void PushType(Type type)
        {
            Gen.EmitConstant(type);
            Gen.EmitCall(GetTypeFromHandle);
        }

        private void PushMethod(MethodInfo method)
        {
            Gen.Emit(OpCodes.Ldtoken, method);

            if (method.DeclaringType != null && method.DeclaringType.IsGenericType)
            {
                Gen.EmitConstant(method.DeclaringType);
                Gen.EmitCall(GetMethodFromHandleOfType);
            }
            else
            {
                Gen.EmitCall(GetMethodFromHandle);
            }

            Gen.EmitCast(typeof(MethodInfo));
        }

        private void PushConstructor(ConstructorInfo ctor)
        {
            Gen.Emit(OpCodes.Ldtoken, ctor);

            if (ctor.DeclaringType != null && ctor.DeclaringType.IsGenericType)
            {
                Gen.EmitConstant(ctor.DeclaringType);
                Gen.EmitCall(GetMethodFromHandleOfType);
            }
            else
            {
                Gen.EmitCall(GetMethodFromHandle);
            }

            Gen.EmitCast(typeof(ConstructorInfo));
        }

        private void PushField(FieldInfo field)
        {
            Gen.Emit(OpCodes.Ldtoken, field);

            if (field.DeclaringType != null && field.DeclaringType.IsGenericType)
            {
                Gen.EmitConstant(field.DeclaringType);
                Gen.EmitCall(GetFieldFromHandleOfType);
            }
            else
            {
                Gen.EmitCall(GetFieldFromHandle);
            }
        }

        /// <summary>
        /// Declares an IL local of the enclosing method to hold a piece of the tree being built.
        /// </summary>
        private LocalBuilder DeclareTemp(Type type)
        {
            return Gen.DeclareLocal(type);
        }

        private NodeBase SingleStatementOf(CodeBlockNode block)
        {
            var statements = block.Statements.Where(x => !(x is IMetaNode)).ToArray();
            if (statements.Length != 1)
                Error(block, CompilerMessages.ExpressionTreeBlockBody);

            return statements[0];
        }

        /// <summary>
        /// Names a node in the way the author would recognise it.
        /// </summary>
        private static string Describe(NodeBase node)
        {
            switch (node)
            {
                case SetIdentifierNode _:
                case SetMemberNode _:
                case SetIndexNode _:
                case ShortAssignmentNode _: return "assignment";
                case VarNode _:
                case LetNode _: return "variable declaration";
                case LambdaNode _: return "nested lambda";
                default: return node.GetType().Name.Replace("Node", string.Empty).ToLowerInvariant();
            }
        }

        private void Error(LocationEntity entity, string message, params object[] args)
        {
            throw new LensCompilerException(string.Format(message, args), entity);
        }

        #endregion

        #region Reflection cache

        private static readonly Type Expr = typeof(Expression);

        private static readonly MethodInfo GetTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", new[] {typeof(RuntimeTypeHandle)});
        private static readonly MethodInfo GetMethodFromHandle = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] {typeof(RuntimeMethodHandle)});
        private static readonly MethodInfo GetMethodFromHandleOfType = typeof(MethodBase).GetMethod("GetMethodFromHandle", new[] {typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)});
        private static readonly MethodInfo GetFieldFromHandle = typeof(FieldInfo).GetMethod("GetFieldFromHandle", new[] {typeof(RuntimeFieldHandle)});
        private static readonly MethodInfo GetFieldFromHandleOfType = typeof(FieldInfo).GetMethod("GetFieldFromHandle", new[] {typeof(RuntimeFieldHandle), typeof(RuntimeTypeHandle)});

        private static readonly MethodInfo StringConcat = typeof(string).GetMethod("Concat", new[] {typeof(string), typeof(string)});

        private static readonly MethodInfo FactoryParameter =Expr.GetMethod("Parameter", new[] {typeof(Type), typeof(string)});
        private static readonly MethodInfo FactoryConstant = Expr.GetMethod("Constant", new[] {typeof(object), typeof(Type)});
        private static readonly MethodInfo FactoryField = Expr.GetMethod("Field", new[] {typeof(Expression), typeof(FieldInfo)});
        private static readonly MethodInfo FactoryProperty = Expr.GetMethod("Property", new[] {typeof(Expression), typeof(MethodInfo)});
        private static readonly MethodInfo FactoryArrayLength = Expr.GetMethod("ArrayLength", new[] {typeof(Expression)});
        private static readonly MethodInfo FactoryArrayIndex = Expr.GetMethod("ArrayIndex", new[] {typeof(Expression), typeof(Expression)});
        private static readonly MethodInfo FactoryCallInstance = Expr.GetMethod("Call", new[] {typeof(Expression), typeof(MethodInfo), typeof(Expression[])});
        private static readonly MethodInfo FactoryCallStatic = Expr.GetMethod("Call", new[] {typeof(MethodInfo), typeof(Expression[])});
        private static readonly MethodInfo FactoryNew = Expr.GetMethod("New", new[] {typeof(ConstructorInfo), typeof(Expression[])});
        private static readonly MethodInfo FactoryNewArrayInit = Expr.GetMethod("NewArrayInit", new[] {typeof(Type), typeof(Expression[])});
        private static readonly MethodInfo FactoryCondition = Expr.GetMethod("Condition", new[] {typeof(Expression), typeof(Expression), typeof(Expression)});
        private static readonly MethodInfo FactoryConvert = Expr.GetMethod("Convert", new[] {typeof(Expression), typeof(Type)});
        private static readonly MethodInfo FactoryTypeIs = Expr.GetMethod("TypeIs", new[] {typeof(Expression), typeof(Type)});
        private static readonly MethodInfo FactoryDefault = Expr.GetMethod("Default", new[] {typeof(Type)});
        private static readonly MethodInfo FactoryCoalesce = Expr.GetMethod("Coalesce", new[] {typeof(Expression), typeof(Expression)});

        private static readonly MethodInfo FactoryAndAlso = Expr.GetMethod("AndAlso", new[] {typeof(Expression), typeof(Expression)});
        private static readonly MethodInfo FactoryOrElse = Expr.GetMethod("OrElse", new[] {typeof(Expression), typeof(Expression)});
        private static readonly MethodInfo FactoryNot = Expr.GetMethod("Not", new[] {typeof(Expression)});
        private static readonly MethodInfo FactoryNegate = Expr.GetMethod("Negate", new[] {typeof(Expression)});
        private static readonly MethodInfo FactoryNegateMethod = Expr.GetMethod("Negate", new[] {typeof(Expression), typeof(MethodInfo)});

        private static readonly MethodInfo FactoryAdd = Binary("Add");
        private static readonly MethodInfo FactorySubtract = Binary("Subtract");
        private static readonly MethodInfo FactoryMultiply = Binary("Multiply");
        private static readonly MethodInfo FactoryDivide = Binary("Divide");
        private static readonly MethodInfo FactoryModulo = Binary("Modulo");
        private static readonly MethodInfo FactoryPower = Binary("Power");
        private static readonly MethodInfo FactoryAnd = Binary("And");
        private static readonly MethodInfo FactoryOr = Binary("Or");
        private static readonly MethodInfo FactoryExclusiveOr = Binary("ExclusiveOr");
        private static readonly MethodInfo FactoryEqual = Binary("Equal");
        private static readonly MethodInfo FactoryNotEqual = Binary("NotEqual");
        private static readonly MethodInfo FactoryLessThan = Binary("LessThan");
        private static readonly MethodInfo FactoryLessThanOrEqual = Binary("LessThanOrEqual");
        private static readonly MethodInfo FactoryGreaterThan = Binary("GreaterThan");
        private static readonly MethodInfo FactoryGreaterThanOrEqual = Binary("GreaterThanOrEqual");

        private static readonly MethodInfo FactoryAddMethod = BinaryWithMethod("Add");
        private static readonly MethodInfo FactorySubtractMethod = BinaryWithMethod("Subtract");
        private static readonly MethodInfo FactoryMultiplyMethod = BinaryWithMethod("Multiply");
        private static readonly MethodInfo FactoryDivideMethod = BinaryWithMethod("Divide");
        private static readonly MethodInfo FactoryModuloMethod = BinaryWithMethod("Modulo");
        private static readonly MethodInfo FactoryLessThanMethod = BinaryWithMethod("LessThan");
        private static readonly MethodInfo FactoryLessThanOrEqualMethod = BinaryWithMethod("LessThanOrEqual");
        private static readonly MethodInfo FactoryGreaterThanMethod = BinaryWithMethod("GreaterThan");
        private static readonly MethodInfo FactoryGreaterThanOrEqualMethod = BinaryWithMethod("GreaterThanOrEqual");

        // the equality factories interpose a lifting flag before the method, unlike every other one
        private static readonly MethodInfo FactoryEqualLifted = Expr.GetMethod("Equal", new[] {typeof(Expression), typeof(Expression), typeof(bool), typeof(MethodInfo)});
        private static readonly MethodInfo FactoryNotEqualLifted = Expr.GetMethod("NotEqual", new[] {typeof(Expression), typeof(Expression), typeof(bool), typeof(MethodInfo)});

        private static readonly MethodInfo FactoryLambda = Expr.GetMethods()
                                                               .Single(
                                                                   m => m.Name == "Lambda"
                                                                        && m.IsGenericMethodDefinition
                                                                        && m.GetGenericArguments().Length == 1
                                                                        && MatchesParameters(m, typeof(Expression), typeof(ParameterExpression[]))
                                                               );

        private static MethodInfo Binary(string name)
        {
            return Expr.GetMethod(name, new[] {typeof(Expression), typeof(Expression)});
        }

        private static MethodInfo BinaryWithMethod(string name)
        {
            return Expr.GetMethod(name, new[] {typeof(Expression), typeof(Expression), typeof(MethodInfo)});
        }

        private static bool MatchesParameters(MethodInfo method, params Type[] types)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != types.Length)
                return false;

            for (var idx = 0; idx < types.Length; idx++)
                if (parameters[idx].ParameterType != types[idx])
                    return false;

            return true;
        }

        #endregion
    }
}
