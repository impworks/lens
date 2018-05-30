using System;
using System.Collections;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.Binary
{
    /// <summary>
    /// An operator node that adds two values together.
    /// </summary>
    internal class AddOperatorNode : BinaryOperatorNodeBase
    {
        #region Operator basics

        protected override string OperatorRepresentation => "+";

        protected override string OverloadedMethodName => "op_Addition";

        protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new AddOperatorNode {LeftOperand = left, RightOperand = right};
        }

        #endregion

        #region Resolve

        protected override Type ResolveOperatorType(Context ctx, Type leftType, Type rightType)
        {
            var stringyTypes = new[] {typeof(string), typeof(char)};
            if (leftType.IsAnyOf(stringyTypes) && rightType.IsAnyOf(stringyTypes))
                return typeof(string);

            if (leftType == rightType)
            {
                if (leftType.IsArray || leftType.IsAppliedVersionOf(typeof(Dictionary<,>)))
                    return leftType;
            }

            var dictType = typeof(IDictionary<,>).ResolveCommonImplementationFor(leftType, rightType);
            if (dictType != null)
                return dictType;

            var enumerableType = typeof(IEnumerable<>).ResolveCommonImplementationFor(leftType, rightType)
                                 ?? typeof(IEnumerable).ResolveCommonImplementationFor(leftType, rightType);

            if (enumerableType != null)
                return enumerableType;

            return null;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            if (!IsConstant)
            {
                var type = Resolve(ctx);

                if (type == typeof(string))
                    return StringExpand();

                if (type.IsArray)
                    return ArrayExpand(ctx);

                if (type.IsAppliedVersionOf(typeof(IDictionary<,>)))
                    return DictExpand(ctx);

                if (type == typeof(IEnumerable))
                    return SeqExpand();

                if (type.IsAppliedVersionOf(typeof(IEnumerable<>)))
                    return TypedSeqExpand();
            }

            return base.Expand(ctx, mustReturn);
        }

        /// <summary>
        /// Returns the code to concatenate two strings.
        /// </summary>
        private NodeBase StringExpand()
        {
            return Expr.Invoke("string", "Concat", LeftOperand, RightOperand);
        }

        /// <summary>
        /// Returns the code to concatenate two arrays.
        /// </summary>
        private NodeBase ArrayExpand(Context ctx)
        {
            var type = Resolve(ctx);

            var tmpArray = ctx.Scope.DeclareImplicit(ctx, type, false);
            var tmpLeft = ctx.Scope.DeclareImplicit(ctx, type, false);
            var tmpRight = ctx.Scope.DeclareImplicit(ctx, type, false);

            // a = <left>
            // b = <right>
            // c = new T[a.Length + b.Length]
            // Array.Copy(from: a, to: c, count: a.Length)
            // Array.Copy(from: b, startFrom: 0, to: c, startTo: a.Length, count: b.Length)
            return Expr.Block(
                Expr.Set(tmpLeft, LeftOperand),
                Expr.Set(tmpRight, RightOperand),
                Expr.Set(
                    tmpArray,
                    Expr.Array(
                        type.GetElementType(),
                        Expr.Add(
                            Expr.GetMember(Expr.Get(tmpLeft), "Length"),
                            Expr.GetMember(Expr.Get(tmpRight), "Length")
                        )
                    )
                ),
                Expr.Invoke(
                    "System.Array",
                    "Copy",
                    Expr.Get(tmpLeft),
                    Expr.Get(tmpArray),
                    Expr.GetMember(Expr.Get(tmpLeft), "Length")
                ),
                Expr.Invoke(
                    "System.Array",
                    "Copy",
                    Expr.Get(tmpRight),
                    Expr.Int(0),
                    Expr.Get(tmpArray),
                    Expr.GetMember(Expr.Get(tmpLeft), "Length"),
                    Expr.GetMember(Expr.Get(tmpRight), "Length")
                ),
                Expr.Get(tmpArray)
            );
        }

        /// <summary>
        /// Returns the code to concatenate two untyped value sequences.
        /// </summary>
        private NodeBase SeqExpand()
        {
            // a.OfType<object>().Concat(b.OfType<object>())
            return Expr.Invoke(
                "System.Linq.Enumerable",
                "Concat",
                Expr.Invoke(
                    Expr.GetMember(
                        "System.Linq.Enumerable",
                        "OfType",
                        "object"
                    ),
                    LeftOperand
                ),
                Expr.Invoke(
                    Expr.GetMember(
                        "System.Linq.Enumerable",
                        "OfType",
                        "object"
                    ),
                    RightOperand
                )
            );
        }

        /// <summary>
        /// Returns the code to concatenate two typed value sequences.
        /// </summary>
        private NodeBase TypedSeqExpand()
        {
            return Expr.Invoke("System.Linq.Enumerable", "Concat", LeftOperand, RightOperand);
        }

        /// <summary>
        /// Returns the code to concatenate two dictionaries.
        /// </summary>
        private NodeBase DictExpand(Context ctx)
        {
            var keyValueTypes = LeftOperand.Resolve(ctx).GetGenericArguments();
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyValueTypes);
            var currType = typeof(KeyValuePair<,>).MakeGenericType(keyValueTypes);
            var tmpDict = ctx.Scope.DeclareImplicit(ctx, dictType, false);
            var tmpCurr = ctx.Scope.DeclareImplicit(ctx, currType, false);

            // a = new Dictionary<T, T2>(<left>)
            // foreach(var kvp in <right>)
            //    a[kvp.Key] = kvp.Value
            return Expr.Block(
                Expr.Set(
                    tmpDict,
                    Expr.New(
                        dictType,
                        Expr.Cast(
                            LeftOperand,
                            typeof(IDictionary<,>).MakeGenericType(keyValueTypes)
                        )
                    )
                ),
                Expr.For(
                    tmpCurr,
                    RightOperand,
                    Expr.Block(
                        Expr.SetIdx(
                            Expr.Get(tmpDict),
                            Expr.GetMember(
                                Expr.Get(tmpCurr),
                                "Key"
                            ),
                            Expr.GetMember(
                                Expr.Get(tmpCurr),
                                "Value"
                            )
                        )
                    )
                ),
                Expr.Get(tmpDict)
            );
        }

        #endregion

        #region Emit

        protected override void EmitOperator(Context ctx)
        {
            LoadAndConvertNumerics(ctx);
            ctx.CurrentMethod.Generator.EmitAdd();
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic left, dynamic right)
        {
            if (left is char && right is char)
                return string.Concat(left, right);

            try
            {
                return checked(left + right);
            }
            catch (OverflowException)
            {
                Error(CompilerMessages.ConstantOverflow);
                return null;
            }
        }

        #endregion
    }
}