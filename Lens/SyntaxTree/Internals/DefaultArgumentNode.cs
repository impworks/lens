using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// The value a callee declared for a parameter the call site left out.
    ///
    /// It is a constant of the parameter's own type rather than of the type the value is spelled
    /// with, which is what tells it apart from an ordinary literal: metadata records the default of
    /// a byte, an enum or a char as the integer it is, and the call needs the parameter's type on
    /// the stack. A default of null stands for whatever 'default' means for the type - the null
    /// reference, the zeroed struct, the empty nullable.
    /// </summary>
    internal class DefaultArgumentNode : NodeBase
    {
        #region Constructor

        public DefaultArgumentNode(object value, TypeEntry type)
        {
            _value = value;
            _type = type;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The default value, as metadata records it.
        /// </summary>
        private readonly object _value;

        /// <summary>
        /// The type of the parameter the value is passed to.
        /// </summary>
        private readonly TypeEntry _type;

        #endregion

        #region Constant checkers

        // the value is known before anything is emitted, which is what lets a call carrying one be
        // built into an expression tree
        public override bool IsConstant => true;
        public override object ConstantValue => _value;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            return _type;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            if (_value == null)
            {
                Expr.Default(_type).Emit(ctx, true);
                return;
            }

            var gen = ctx.CurrentMethod.Generator;
            var type = _type.Materialize();

            // an enum is its underlying integer on the stack, and nothing distinguishes the two
            // there: the metadata says which enum it is, and the call site is where that is known
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    gen.EmitConstant(Convert.ToBoolean(_value));
                    return;

                case TypeCode.String:
                    gen.EmitConstant(Convert.ToString(_value));
                    return;

                case TypeCode.Single:
                    gen.EmitConstant(Convert.ToSingle(_value));
                    return;

                case TypeCode.Double:
                    gen.EmitConstant(Convert.ToDouble(_value));
                    return;

                case TypeCode.Decimal:
                    gen.EmitConstant(Convert.ToDecimal(_value));
                    return;

                // everything integral narrower than 8 bytes is an i4 on the stack, exactly as a
                // literal of it would be
                case TypeCode.Char:
                    gen.EmitConstant(Convert.ToInt32(Convert.ToChar(_value)));
                    return;

                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                    gen.EmitConstant(Convert.ToInt32(_value));
                    return;

                case TypeCode.UInt32:
                    gen.EmitConstant(unchecked((int) Convert.ToUInt32(_value)));
                    return;

                case TypeCode.Int64:
                    gen.EmitConstant(Convert.ToInt64(_value));
                    return;

                case TypeCode.UInt64:
                    gen.EmitConstant(unchecked((long) Convert.ToUInt64(_value)));
                    return;

                default:
                    throw new InvalidOperationException($"A default value of type {type} cannot be emitted!");
            }
        }

        #endregion

        #region Debug

        protected bool Equals(DefaultArgumentNode other)
        {
            return Equals(_value, other._value) && Equals(_type, other._type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DefaultArgumentNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_value != null ? _value.GetHashCode() : 0) * 397) ^ (_type != null ? _type.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return $"default({_type}, {_value ?? "null"})";
        }

        #endregion
    }
}
