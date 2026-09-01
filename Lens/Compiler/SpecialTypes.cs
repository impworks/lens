namespace Lens.Compiler
{
    /// <summary>
    /// A pseudotype to represent the absense of value (void).
    /// </summary>
    internal class UnitType
    {
        public override string ToString()
        {
            return "()";
        }

        #region Debug

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType();
        }

        public override int GetHashCode()
        {
            return 0;
        }

        #endregion
    }

    /// <summary>
    /// A pseudotype to represent the null variable.
    /// </summary>
    internal class NullType
    {
    }

    /// <summary>
    /// A pseudotype for expressions which type is to be resolved later: partial application placeholders and lambda arguments.
    /// </summary>
    internal class UnspecifiedType
    {
    }

    /// <summary>
    /// The type of a lambda literal that has not yet been told which delegate it is to become.
    ///
    /// The shape mirrors <see cref="System.Func{TResult}"/> - the arguments, then the result - and
    /// says only what the literal itself knows. A lambda written without argument types knows
    /// neither, and carries <see cref="UnspecifiedType"/> in their place; one written with them knows
    /// its result too, and that is what tells apart two overloads differing only in what the
    /// selector returns.
    ///
    /// It exists because a lambda is not a delegate of any particular family until something says
    /// which: the same literal reaches a Func, a Converter and a Predicate, and committing it to
    /// one of them on sight is what used to make the other two unreachable.
    /// </summary>
    internal delegate TResult Lambda<out TResult>();
    internal delegate TResult Lambda<in T1, out TResult>(T1 arg1);
    internal delegate TResult Lambda<in T1, in T2, out TResult>(T1 arg1, T2 arg2);
    internal delegate TResult Lambda<in T1, in T2, in T3, out TResult>(T1 arg1, T2 arg2, T3 arg3);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, in T11, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, in T11, in T12, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, in T11, in T12, in T13, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, in T11, in T12, in T13, in T14, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, in T11, in T12, in T13, in T14, in T15, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15);
    internal delegate TResult Lambda<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10, in T11, in T12, in T13, in T14, in T15, in T16, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16);
}