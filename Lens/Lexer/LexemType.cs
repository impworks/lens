﻿namespace Lens.Lexer
{
    internal enum LexemType
    {
        Eof,
        NewLine,

        // Keywords
        Use,
        Using,
        Type,
        Record,
        Pure,
        Fun,
        If,
        Then,
        Else,
        For,
        In,
        While,
        Do,
        Try,
        Catch,
        Finally,
        Match,
        With,
        Case,
        When,
        Let,
        Var,
        New,
        Not,
        Is,
        As,
        Of,
        Typeof,
        Default,
        Ref,
        Throw,

        // Literals
        Null,
        Unit,
        True,
        False,
        Int,
        Long,
        Float,
        Double,
        Decimal,
        Char,
        String,
        Identifier,
        Regex,

        // Braces
        ParenOpen,
        ParenClose,
        CurlyOpen,
        CurlyClose,
        SquareOpen,
        SquareClose,

        // Operators
        Plus,
        Minus,
        Multiply,
        Divide,
        Power,
        Remainder,
        Equal,
        NotEqual,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
        BitAnd,
        BitOr,
        BitXor,
        And,
        Or,
        Xor,
        ShiftLeft,
        ShiftRight,
        Assign,

        // Specials
        PassLeft,
        PassRight,
        Arrow,
        FatArrow,
        DoubleDot,
        Dot,
        Colon,
        DoubleСolon,
        Semicolon,
        Comma,
        Tilde,
        QuestionMark,
        Ellipsis,

        Indent,
        Dedent
    }
}