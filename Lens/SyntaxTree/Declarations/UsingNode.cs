﻿using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Declarations
{
    /// <summary>
    /// A block that acquires and releases a resource.
    /// </summary>
    internal class UsingNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// A variable to assign the resource to.
        /// </summary>
        public string VariableName { get; set; }

        /// <summary>
        /// An expression of IDisposable type.
        /// </summary>
        public NodeBase Expression { get; set; }

        /// <summary>
        /// Statements in the block.
        /// </summary>
        public CodeBlockNode Body { get; set; }

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            var exprType = Expression.Resolve(ctx, mustReturn);
            if (!typeof(IDisposable).IsAssignableFrom(exprType))
                Error(Expression, CompilerMessages.ExpressionNotIDisposable, exprType);

            if (VariableName != null && ctx.Scope.FindLocal(VariableName) != null)
                throw new LensCompilerException(string.Format(CompilerMessages.VariableDefined, VariableName));

            if (!mustReturn)
                return typeof(UnitType);

            return string.IsNullOrEmpty(VariableName)
                ? Body.Resolve(ctx)
                : Scope.WithTempLocals(ctx, () => Body.Resolve(ctx), new Local(VariableName, exprType));
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var exprType = Expression.Resolve(ctx, mustReturn);
            var tmpVar = ctx.Scope.DeclareImplicit(ctx, exprType, false);

            var newBody = Expr.Block(Expr.Set(tmpVar, Expression));

            if (!string.IsNullOrEmpty(VariableName))
                newBody.Add(Expr.Let(VariableName, Expr.Get(tmpVar)));

            newBody.Add(Body);

            return Expr.Try(
                newBody,
                Expr.Block(
                    Expr.Invoke(Expr.Get(tmpVar), "Dispose")
                )
            );
        }

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression, x => Expression = x);
            yield return new NodeChild(Body, null);
        }

        #endregion

        #region Debug

        protected bool Equals(UsingNode other)
        {
            return string.Equals(VariableName, other.VariableName) && Equals(Expression, other.Expression) && Equals(Body, other.Body);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((UsingNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (VariableName != null ? VariableName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Body != null ? Body.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"using(var = ({VariableName}), expr = ({Expression}), body = ({Body}))";
        }

        #endregion
    }
}