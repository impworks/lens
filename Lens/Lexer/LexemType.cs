﻿namespace Lens.Lexer
{
	public enum LexemType
	{
		EOF,
		NewLine,

		// Keywords
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

		// Literals
		Null,
		Unit,
		True,
		False,
		Int,
		Double,
		String,
		Char,
		Identifier,

		// Braces
		ParenOpen,
		ParenClose,
		CurlyOpen,
		CurlyClose,
		SquareOpen,
		SquareClose,
		DoubleSquareOpen,
		DoubleSquareClose,

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
		And,
		Or,
		Xor,
		Assign,

		// Specials
		ArrayDef,
		MoveLeft,
		MoveRight,
		Arrow,
		FatArrow,
		DoubleDot,
		Dot,
		Colon,
		DoubleСolon,
		Semicolon,
		Comma,
		Indent,
		Dedent
	}
}