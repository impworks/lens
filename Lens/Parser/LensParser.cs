using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Lens.Compiler;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Declarations.Locals;
using Lens.SyntaxTree.Declarations.Types;
using Lens.SyntaxTree.Expressions;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.Expressions.Instantiation;
using Lens.SyntaxTree.Literals;
using Lens.SyntaxTree.Operators.TypeBased;
using Lens.Translations;

namespace Lens.Parser
{
	using System.Data;

	using Lens.SyntaxTree.PatternMatching;
	using Lens.SyntaxTree.PatternMatching.Rules;


	internal partial class LensParser
	{
		#region Constructor

		public LensParser(IEnumerable<Lexem> lexems)
		{
			_lexems = lexems.ToArray();

			Nodes = ParseMain().ToList();
		}

		#endregion

		#region Fields

		/// <summary>
		/// Generated list of nodes.
		/// </summary>
		public List<NodeBase> Nodes { get; private set; }

		/// <summary>
		/// Source list of lexems.
		/// </summary>
		private readonly Lexem[] _lexems;

		/// <summary>
		/// Current index of the lexem in the stream.
		/// </summary>
		private int _lexemId;

		#endregion

		#region Globals

		/// <summary>
		/// main                                        = stmt { NL stmt } EOF
		/// </summary>
		private IEnumerable<NodeBase> ParseMain()
		{
			yield return ParseStmt();

			while (!Check(LexemType.Eof))
			{
				if (!IsStmtSeparator())
				{
					if(Peek(LexemType.Assign))
						Error(ParserMessages.AssignLvalueExpected);
					else
						Error(ParserMessages.NewlineSeparatorExpected);
				}

				yield return ParseStmt();
			}
		}

		/// <summary>
		/// stmt                                        = using | record_def | type_def | fun_def | local_stmt
		/// </summary>
		private NodeBase ParseStmt()
		{
			return Attempt(ParseUsing)
				   ?? Attempt(ParseRecordDef)
				   ?? Attempt(ParseTypeDef)
				   ?? Attempt(ParseFunDef)
				   ?? Ensure(ParseLocalStmt, ParserMessages.UnknownStatement);
		}

		#endregion

		#region Namespace & type signatures

		/// <summary>
		/// namespace                                   = identifier { "." identifier }
		/// </summary>
		private TypeSignature ParseNamespace()
		{
			return Bind(() =>
				{
					if (!Peek(LexemType.Identifier))
						return null;

					var identifier = GetValue();
					if (!Peek(LexemType.Dot))
						return new TypeSignature(identifier);

					var sb = new StringBuilder(identifier);
					while (Check(LexemType.Dot))
					{
						identifier = Ensure(LexemType.Identifier, ParserMessages.IdentifierExpected).Value;
						sb.Append(".");
						sb.Append(identifier);
					}

					return new TypeSignature(sb.ToString());
				}
			);
		}

		/// <summary>
		/// type                                        = namespace [ type_args ] { "[]" | "?" | "~" }
		/// </summary>
		private TypeSignature ParseType()
		{
			var node = Attempt(ParseNamespace);
			if (node == null)
				return null;

			var args = Attempt(ParseTypeArgs);
			if(args != null)
				node = new TypeSignature(node.Name, args.ToArray());

			while (true)
			{
				if (Peek(LexemType.SquareOpen, LexemType.SquareClose))
				{
					node = new TypeSignature(null, "[]", node);
					Skip(2);
				}

				else if (Check(LexemType.Tilde))
					node = new TypeSignature(null, "~", node);

				else if (Check(LexemType.QuestionMark))
					node = new TypeSignature(null, "?", node);

				else
					return node;
			}
		}

		/// <summary>
		/// type_args                                   = "<" type { "," type } ">"
		/// </summary>
		private List<TypeSignature> ParseTypeArgs()
		{
			if (!Check(LexemType.Less))
				return null;

			var arg = Attempt(ParseType);
			if (arg == null)
				return null;

			if (!PeekAny(new[] {LexemType.Comma, LexemType.Greater}))
				return null;

			var list = new List<TypeSignature> {arg};
			while (Check(LexemType.Comma))
				list.Add(Ensure(ParseType, ParserMessages.TypeArgumentExpected));

			Ensure(LexemType.Greater, ParserMessages.SymbolExpected, '>');
			return list;
		}

		#endregion

		#region Structures

		/// <summary>
		/// using                                       = "using" namespace NL
		/// </summary>
		private UseNode ParseUsing()
		{
			if (!Check(LexemType.Use))
				return null;

			var nsp = Ensure(ParseNamespace, ParserMessages.NamespaceExpected);
			var node = new UseNode {Namespace = nsp.FullSignature};

			return node;
		}

		/// <summary>
		/// record_def                                  = "record" identifier INDENT record_stmt { NL record_stmt } DEDENT
		/// </summary>
		private RecordDefinitionNode ParseRecordDef()
		{
			if (!Check(LexemType.Record))
				return null;

			var node = new RecordDefinitionNode();

			node.Name = Ensure(LexemType.Identifier, ParserMessages.RecordIdentifierExpected).Value;
			Ensure(LexemType.Indent, ParserMessages.RecordIndentExpected);
			
			var field = Bind(ParseRecordStmt);
			node.Entries.Add(field);

			while (!Check(LexemType.Dedent))
			{
				Ensure(LexemType.NewLine, ParserMessages.RecordSeparatorExpected);
				field = Bind(ParseRecordStmt);
				node.Entries.Add(field);
			}

			return node;
		}

		/// <summary>
		/// record_stmt                                 = identifier ":" type
		/// </summary>
		private RecordField ParseRecordStmt()
		{
			var node = new RecordField();

			node.Name = Ensure(LexemType.Identifier, ParserMessages.RecordFieldIdentifierExpected).Value;
			Ensure(LexemType.Colon, ParserMessages.SymbolExpected, ':');
			node.Type = Ensure(ParseType, ParserMessages.RecordFieldTypeExpected);

			return node;
		}

		/// <summary>
		/// type_def                                    = "type" identifier INDENT type_stmt { type_stmt } DEDENT
		/// </summary>
		private TypeDefinitionNode ParseTypeDef()
		{
			if (!Check(LexemType.Type))
				return null;

			var node = new TypeDefinitionNode();

			node.Name = Ensure(LexemType.Identifier, ParserMessages.TypeIdentifierExpected).Value;
			Ensure(LexemType.Indent, ParserMessages.TypeIndentExpected);

			var field = Bind(ParseTypeStmt);
			node.Entries.Add(field);

			while (!Check(LexemType.Dedent))
			{
				Ensure(LexemType.NewLine, ParserMessages.TypeSeparatorExpected);
				field = Bind(ParseTypeStmt);
				node.Entries.Add(field);
			}

			return node;
		}

		/// <summary>
		/// type_stmt                                   = identifier [ "of" type ]
		/// </summary>
		private TypeLabel ParseTypeStmt()
		{
			var node = new TypeLabel();

			node.Name = Ensure(LexemType.Identifier, ParserMessages.TypeLabelIdentifierExpected).Value;
			if (Check(LexemType.Of))
				node.TagType = Ensure(ParseType, ParserMessages.TypeLabelTagTypeExpected);

			return node;
		}

		/// <summary>
		/// fun_def                                     = [ "pure" ] "fun" identifier [ ":" type ] fun_args "->" block
		/// </summary>
		private FunctionNode ParseFunDef()
		{
			var node = new FunctionNode();
			node.IsPure = Check(LexemType.Pure);

			if (!Check(LexemType.Fun))
			{
				if (node.IsPure)
					Error(ParserMessages.FunctionDefExpected);
				else
					return null;
			}

			node.Name = Ensure(LexemType.Identifier, ParserMessages.FunctionIdentifierExpected).Value;
			if (Check(LexemType.Colon))
				node.ReturnTypeSignature = Ensure(ParseType, ParserMessages.FunctionReturnExpected);

			node.Arguments = Attempt(() => ParseFunArgs(true)) ?? new List<FunctionArgument>();
			Ensure(LexemType.Arrow, ParserMessages.SymbolExpected, "->");
			node.Body.LoadFrom(Ensure(ParseBlock, ParserMessages.FunctionBodyExpected));

			return node;
		}

		/// <summary>
		/// fun_args                                    = fun_single_arg | fun_many_args
		/// </summary>
		private List<FunctionArgument> ParseFunArgs(bool required = false)
		{
			var single = Attempt(() => ParseFunSingleArg(required));
			if (single != null)
				return new List<FunctionArgument> {single};

			return ParseFunManyArgs(required);
		}

		/// <summary>
		/// fun_arg                                     = identifier [ ":" [ "ref" ] type [ "... " ] ]
		/// </summary>
		private FunctionArgument ParseFunSingleArg(bool required = false)
		{
			if (!Peek(LexemType.Identifier))
				return null;

			var node = new FunctionArgument();
			node.Name = GetValue();

			if (!Check(LexemType.Colon))
			{
				if (required)
					Error(ParserMessages.SymbolExpected, ":");

				node.Type = typeof (UnspecifiedType);
				return node;
			}

			node.IsRefArgument = Check(LexemType.Ref);
			node.TypeSignature = Ensure(ParseType, ParserMessages.ArgTypeExpected);

			if (Check(LexemType.Ellipsis))
			{
				if(node.IsRefArgument)
					Error(ParserMessages.VariadicByRef);

				node.IsVariadic = true;
			}

			return node;
		}

		/// <summary>
		/// fun_arg_list                                = "(" { fun_single_arg } ")"
		/// </summary>
		private List<FunctionArgument> ParseFunManyArgs(bool required = false)
		{
			if (!Check(LexemType.ParenOpen))
				return null;

			var args = new List<FunctionArgument>();
			while (!Check(LexemType.ParenClose))
			{
				var arg = Attempt(() => ParseFunSingleArg(required));
				if (arg == null)
					return null;

				args.Add(arg);
			}

			return args;
		}

		#endregion

		#region Blocks

		/// <summary>
		/// block                                       = local_stmt_list | local_stmt
		/// </summary>
		private CodeBlockNode ParseBlock()
		{
			var many = ParseLocalStmtList().ToList();
			if (many.Count > 0)
				return new CodeBlockNode { Statements = many };

			var single = ParseLocalStmt();
			if (single != null)
				return new CodeBlockNode { single };

			return null;
		}

		/// <summary>
		/// local_stmt_list                             = INDENT local_stmt { NL local_stmt } DEDENT
		/// </summary>
		private IEnumerable<NodeBase> ParseLocalStmtList()
		{
			if (!Check(LexemType.Indent))
				yield break;

			yield return Ensure(ParseLocalStmt, ParserMessages.ExpressionExpected);

			while (!Check(LexemType.Dedent))
			{
				if(!IsStmtSeparator())
					Error(ParserMessages.NewlineSeparatorExpected);
				yield return Ensure(ParseLocalStmt, ParserMessages.ExpressionExpected);
			}
		}

		/// <summary>
		/// local_stmt                                  = name_def_stmt | set_stmt | expr
		/// </summary>
		private NodeBase ParseLocalStmt()
		{
			if(Peek(LexemType.PassLeft))
				Error(ParserMessages.ArgumentPassIndentExpected);

			if (Peek(LexemType.PassRight))
				Error(ParserMessages.MethodPassIndentExpected);

			return Attempt(ParseNameDefStmt)
				   ?? Attempt(ParseSetStmt)
				   ?? Attempt(ParseExpr);
		}

		#endregion

		#region Let & var

		/// <summary>
		/// name_def_stmt                               = var_stmt | let_stmt
		/// </summary>
		private NameDeclarationNodeBase ParseNameDefStmt()
		{
			return Attempt(ParseVarStmt)
				   ?? Attempt(ParseLetStmt) as NameDeclarationNodeBase;
		}

		/// <summary>
		/// var_stmt                                    = "var" identifier ( "=" expr | ":" type )
		/// </summary>
		private VarNode ParseVarStmt()
		{
			if (!Check(LexemType.Var))
				return null;

			var node = new VarNode();
			node.Name = Ensure(LexemType.Identifier, ParserMessages.VarIdentifierExpected).Value;
			if (Check(LexemType.Colon))
				node.Type = Ensure(ParseType, ParserMessages.VarTypeExpected);
			else if(Check(LexemType.Assign))
				node.Value = Ensure(ParseExpr, ParserMessages.InitExpressionExpected);
			else
				Error(ParserMessages.InitExpressionOrTypeExpected);

			return node;
		}

		/// <summary>
		/// let_stmt                                    = "let" identifier "=" expr
		/// </summary>
		private LetNode ParseLetStmt()
		{
			if (!Check(LexemType.Let))
				return null;

			var node = new LetNode();
			node.Name = Ensure(LexemType.Identifier, ParserMessages.VarIdentifierExpected).Value;
			Ensure(LexemType.Assign, ParserMessages.SymbolExpected, '=');
			node.Value = Ensure(ParseExpr, ParserMessages.InitExpressionExpected);

			return node;
		}

		#endregion

		#region Assignment

		/// <summary>
		/// set_stmt                                    = set_id_stmt | set_stmbr_stmt | set_any_stmt
		/// </summary>
		private NodeBase ParseSetStmt()
		{
			return Attempt(ParseSetIdStmt)
				   ?? Attempt(ParseSetStmbrStmt)
				   ?? Attempt(ParseSetAnyStmt);
		}

		/// <summary>
		/// set_id_stmt                                 = identifier assignment_op expr
		/// </summary>
		private NodeBase ParseSetIdStmt()
		{
			if (!Peek(LexemType.Identifier))
				return null;

			var node = new SetIdentifierNode();
			node.Identifier = GetValue();

			if (Check(LexemType.Assign))
			{
				node.Value = Ensure(ParseExpr, ParserMessages.ExpressionExpected);
				return node;
			}

			if (PeekAny(BinaryOperators) && Peek(1, LexemType.Assign))
			{
				var opType = _lexems[_lexemId].Type;
				Skip(2);
				node.Value = Ensure(ParseExpr, ParserMessages.ExpressionExpected);
				return new ShortAssignmentNode(opType, node);
			}

			return null;
		}

		/// <summary>
		/// set_stmbr_stmt                              = type "::" identifier assignment_op expr
		/// </summary>
		private NodeBase ParseSetStmbrStmt()
		{
			var type = Attempt(ParseType);
			if (type == null)
				return null;

			if (!Check(LexemType.DoubleСolon))
				return null;

			var node = new SetMemberNode();
			node.StaticType = type;
			node.MemberName = Ensure(LexemType.Identifier, ParserMessages.MemberNameExpected).Value;

			if (Check(LexemType.Assign))
			{
				node.Value = Ensure(ParseExpr, ParserMessages.ExpressionExpected);
				return node;
			}

			if (PeekAny(BinaryOperators) && Peek(1, LexemType.Assign))
			{
				var opType = _lexems[_lexemId].Type;
				Skip(2);
				node.Value = Ensure(ParseExpr, ParserMessages.ExpressionExpected);
				return new ShortAssignmentNode(opType, node);
			}

			return null;
		}

		/// <summary>
		/// set_any_stmt                                = lvalue_expr assignment_op expr
		/// </summary>
		private NodeBase ParseSetAnyStmt()
		{
			var node = Attempt(ParseLvalueExpr);
			if (node == null)
				return null;

			if (Check(LexemType.Assign))
			{
				var expr = Ensure(ParseExpr, ParserMessages.AssignExpressionExpected);
				return MakeSetter(node, expr);
			}

			if (PeekAny(BinaryOperators) && Peek(1, LexemType.Assign))
			{
				var opType = _lexems[_lexemId].Type;
				Skip(2);
				var expr = Ensure(ParseExpr, ParserMessages.AssignExpressionExpected);
				return new ShortAssignmentNode(opType, MakeSetter(node, expr));
			}

			return null;
		}

		#endregion

		#region Lvalues

		/// <summary>
		/// lvalue_expr                                 = lvalue_name_expr | lvalue_paren_expr
		/// </summary>
		private NodeBase ParseLvalueExpr()
		{
			return Attempt(ParseLvalueNameExpr)
				   ?? Attempt(ParseLvalueParenExpr);
		}

		/// <summary>
		/// lvalue_name_expr                            = lvalue_name { accessor }
		/// </summary>
		private NodeBase ParseLvalueNameExpr()
		{
			var node = Attempt(ParseLvalueName);
			if (node == null)
				return null;

			while (true)
			{
				var acc = Attempt(ParseAccessor);
				if (acc == null)
					return node;

				node = AttachAccessor(node, acc);
			}
		}

		/// <summary>
		/// lvalue_paren_expr                           = paren_expr accessor { accessor }
		/// </summary>
		private NodeBase ParseLvalueParenExpr()
		{
			var node = Attempt(ParseParenExpr);
			if (node == null)
				return null;

			var acc = Attempt(ParseAccessor);
			if (acc == null)
				return null;

			node = AttachAccessor(node, acc);
			while (true)
			{
				acc = Attempt(ParseAccessor);
				if (acc == null)
					return node;

				node = AttachAccessor(node, acc);
			}
		}

		/// <summary>
		/// lvalue_name                                 = lvalue_stmbr_expr | lvalue_id_expr
		/// </summary>
		private NodeBase ParseLvalueName()
		{
			return Attempt(ParseLvalueStmbrExpr)
				   ?? Attempt(ParseLvalueIdExpr) as NodeBase;
		}

		/// <summary>
		/// lvalue_stmbr_expr                           = type "::" identifier
		/// </summary>
		private GetMemberNode ParseLvalueStmbrExpr()
		{
			var type = Attempt(ParseType);
			if (type == null || !Check(LexemType.DoubleСolon))
				return null;

			var node = new GetMemberNode();
			node.StaticType = type;
			node.MemberName = Ensure(LexemType.Identifier, ParserMessages.MemberNameExpected).Value;
			return node;
		}

		/// <summary>
		/// lvalue_id_expr                              = identifier
		/// </summary>
		private GetIdentifierNode ParseLvalueIdExpr()
		{
			if (!Peek(LexemType.Identifier))
				return null;

			return new GetIdentifierNode(GetValue());
		}

		#endregion

		#region Accessors

		/// <summary>
		/// get_expr                                    = atom { accessor }
		/// </summary>
		private NodeBase ParseGetExpr()
		{
			var node = Attempt(ParseAtom);
			if (node == null)
				return null;

			while (true)
			{
				var acc = Attempt(ParseAccessor);
				if (acc == null)
					return node;

				node = AttachAccessor(node, acc);
			}
		}

		/// <summary>
		/// get_id_expr                                 = identifier [ type_args ]
		/// </summary>
		private GetIdentifierNode ParseGetIdExpr()
		{
			var node = Attempt(ParseLvalueIdExpr);
			return node;

			// todo: type args
		}

		/// <summary>
		/// get_stmbr_expr                              = type "::" identifier [ type_args ]
		/// </summary>
		private GetMemberNode ParseGetStmbrExpr()
		{
			var node = Attempt(ParseLvalueStmbrExpr);
			if (node == null)
				return null;

			var hints = Attempt(ParseTypeArgs);
			if (hints != null)
				node.TypeHints = hints;

			return node;
		}

		/// <summary>
		/// accessor                                    = accessor_idx | accessor_mbr
		/// </summary>
		private NodeBase ParseAccessor()
		{
			return Attempt(ParseAccessorIdx)
				   ?? Attempt(ParseAccessorMbr) as NodeBase;
		}

		/// <summary>
		/// accessor_idx                                = "[" line_expr "]"
		/// </summary>
		private GetIndexNode ParseAccessorIdx()
		{
			if (!Check(LexemType.SquareOpen))
				return null;

			var node = new GetIndexNode();
			node.Index = Ensure(ParseLineExpr, ParserMessages.IndexExpressionExpected);
			Ensure(LexemType.SquareClose, ParserMessages.SymbolExpected, ']');
			return node;
		}

		/// <summary>
		/// accessor_mbr                                = "." identifier [ type_args ]
		/// </summary>
		private GetMemberNode ParseAccessorMbr()
		{
			if (!Check(LexemType.Dot))
				return null;

			var node = new GetMemberNode();
			node.MemberName = Ensure(LexemType.Identifier, ParserMessages.MemberNameExpected).Value;

			var args = Attempt(ParseTypeArgs);
			if (args != null)
				node.TypeHints = args;

			return node;
		}

		#endregion

		#region Expression root

		/// <summary>
		/// expr                                        = block_expr | line_expr
		/// </summary>
		private NodeBase ParseExpr()
		{
			return Attempt(ParseBlockExpr)
				   ?? Attempt(ParseLineExpr);
		}

		#endregion

		#region Block control structures

		/// <summary>
		/// block_expr                                  = if_block | while_block | for_block | using_block | try_stmt | match_block | new_block_expr | invoke_block_expr | invoke_block_pass_expr | lambda_block_expr
		/// </summary>
		private NodeBase ParseBlockExpr()
		{
			return Attempt(ParseIfBlock)
				   ?? Attempt(ParseWhileBlock)
				   ?? Attempt(ParseForBlock)
				   ?? Attempt(ParseUsingBlock)
				   ?? Attempt(ParseTryStmt)
				   ?? Attempt(ParseMatchBlock)
				   ?? Attempt(ParseNewBlockExpr)
				   ?? Attempt(ParseInvokeBlockExpr)
				   ?? Attempt(ParseInvokeBlockPassExpr)
				   ?? Attempt(ParseLambdaBlockExpr) as NodeBase;
		}

		/// <summary>
		/// if_block                                    = if_header block [ NL "else" block ]
		/// </summary>
		private IfNode ParseIfBlock()
		{
			var node = Attempt(ParseIfHeader);
			if (node == null)
				return null;

			node.TrueAction = Ensure(ParseBlock, ParserMessages.ConditionBlockExpected);
			if (Check(LexemType.Else))
				node.FalseAction = Ensure(ParseBlock, ParserMessages.CodeBlockExpected);

			return node;
		}

		/// <summary>
		/// while_block                                 = while_header block
		/// </summary>
		private WhileNode ParseWhileBlock()
		{
			var node = Attempt(ParseWhileHeader);
			if (node == null)
				return null;

			node.Body.LoadFrom(Ensure(ParseBlock, ParserMessages.LoopBodyExpected));
			return node;
		}

		/// <summary>
		/// for_block                                   = for_header block
		/// </summary>
		private ForeachNode ParseForBlock()
		{
			var node = Attempt(ParseForHeader);
			if (node == null)
				return null;

			node.Body = Ensure(ParseBlock, ParserMessages.LoopBodyExpected);
			return node;
		}

		/// <summary>
		/// using_block                                 = using_header block
		/// </summary>
		private UsingNode ParseUsingBlock()
		{
			var node = Attempt(ParseUsingHeader);
			if (node == null)
				return null;

			node.Body = Ensure(ParseBlock, ParserMessages.UsingBodyExpected);
			return node;
		}

		/// <summary>
		/// try_stmt                                    = "try" block catch_stmt_list [ finally_stmt ]
		/// </summary>
		private TryNode ParseTryStmt()
		{
			if (!Check(LexemType.Try))
				return null;

			var node = new TryNode();
			node.Code = Ensure(ParseBlock, ParserMessages.TryBlockExpected);
			node.CatchClauses = ParseCatchStmtList().ToList();
			node.Finally = Attempt(ParseFinallyStmt);

			if (node.Finally == null && node.CatchClauses.Count == 0)
				Error(ParserMessages.CatchExpected);

			return node;
		}

		/// <summary>
		/// catch_stmt_list                             = catch_stmt { catch_stmt }
		/// </summary>
		private IEnumerable<CatchNode> ParseCatchStmtList()
		{
			while (Peek(LexemType.Catch))
				yield return ParseCatchStmt();
		}

		/// <summary>
		/// catch_stmt                                  = "catch" [ identifier ":" type ] block
		/// </summary>
		private CatchNode ParseCatchStmt()
		{
			if (!Check(LexemType.Catch))
				return null;

			var node = new CatchNode();
			if (Peek(LexemType.Identifier))
			{
				node.ExceptionVariable = GetValue();
				Ensure(LexemType.Colon, ParserMessages.SymbolExpected, ':');
				node.ExceptionType = Ensure(ParseType, ParserMessages.ExceptionTypeExpected);
			}

			node.Code = Ensure(ParseBlock, ParserMessages.ExceptionHandlerExpected);
			return node;
		}

		/// <summary>
		/// finally_stmt                                = "finally" block
		/// </summary>
		private CodeBlockNode ParseFinallyStmt()
		{
			if (!Check(LexemType.Finally))
				return null;

			var block = ParseBlock();
			return block;
		}

		/// <summary>
		/// lambda_block_expr                           = [ fun_args ] "->" block
		/// </summary>
		private LambdaNode ParseLambdaBlockExpr()
		{
			var node = new LambdaNode();
			node.Arguments = Attempt(() => ParseFunArgs()) ?? new List<FunctionArgument>();
			
			if (!Check(LexemType.Arrow))
				return null;

			node.Body.LoadFrom(Ensure(ParseBlock, ParserMessages.FunctionBodyExpected));
			return node;
		}

		#endregion

		#region Headers

		/// <summary>
		/// if_header                                   = "if" line_expr "then"
		/// </summary>
		private IfNode ParseIfHeader()
		{
			if (!Check(LexemType.If))
				return null;

			var node = new IfNode();
			node.Condition = Ensure(ParseLineExpr, ParserMessages.ConditionExpected);
			Ensure(LexemType.Then, ParserMessages.SymbolExpected, "then");

			return node;
		}

		/// <summary>
		/// while_header                                = "while" line_expr "do"
		/// </summary>
		private WhileNode ParseWhileHeader()
		{
			if (!Check(LexemType.While))
				return null;

			var node = new WhileNode();
			node.Condition = Ensure(ParseLineExpr, ParserMessages.ConditionExpected);
			Ensure(LexemType.Do, ParserMessages.SymbolExpected, "do");

			return node;
		}

		/// <summary>
		/// for_block                                   = "for" identifier "in" line_expr [ ".." line_expr ] "do"
		/// </summary>
		private ForeachNode ParseForHeader()
		{
			if (!Check(LexemType.For))
				return null;

			var node = new ForeachNode();
			node.VariableName = Ensure(LexemType.Identifier, ParserMessages.VarIdentifierExpected).Value;
			Ensure(LexemType.In, ParserMessages.SymbolExpected, "in");

			var iter = Ensure(ParseLineExpr, ParserMessages.SequenceExpected);
			if (Check(LexemType.DoubleDot))
			{
				node.RangeStart = iter;
				node.RangeEnd = Ensure(ParseLineExpr, ParserMessages.RangeEndExpected);
			}
			else
			{
				node.IterableExpression = iter;
			}

			Ensure(LexemType.Do, ParserMessages.SymbolExpected, "do");
			return node;
		}

		/// <summary>
		/// using_header                                = "using" [ identifier "=" ] line_expr "do"
		/// </summary>
		private UsingNode ParseUsingHeader()
		{
			if (!Check(LexemType.Using))
				return null;

			var node = new UsingNode();
			if (Peek(LexemType.Identifier, LexemType.Assign))
			{
				node.VariableName = GetValue();
				Skip();
			}

			node.Expression = Ensure(ParseLineExpr, ParserMessages.ExpressionExpected);
			Ensure(LexemType.Do, ParserMessages.SymbolExpected, "do");

			return node;
		}

		#endregion

		#region Block initializers

		/// <summary>
		/// new_block_expr                              = "new" new_tuple_block | new_array_block | new_list_block | new_dict_block | new_object_block
		/// </summary>
		private NodeBase ParseNewBlockExpr()
		{
			if (!Check(LexemType.New))
				return null;

			return Attempt(ParseNewTupleBlock)
				   ?? Attempt(ParseNewListBlock)
				   ?? Attempt(ParseNewArrayBlock)
				   ?? Attempt(ParseNewDictBlock)
				   ?? Attempt(ParseNewObjectBlock) as NodeBase;
		}

		/// <summary>
		/// new_tuple_block                             = "(" init_expr_block ")"
		/// </summary>
		private NewTupleNode ParseNewTupleBlock()
		{
			if (!Check(LexemType.ParenOpen))
				return null;

			var node = new NewTupleNode();
			node.Expressions = ParseInitExprBlock().ToList();
			if(node.Expressions.Count == 0)
				return null;

			Ensure(LexemType.ParenClose, ParserMessages.SymbolExpected, ')');

			return node;
		}

		/// <summary>
		/// new_list_block                              = "[" "[" init_expr_block "]" "]"
		/// </summary>
		private NewListNode ParseNewListBlock()
		{
			if (!Peek(LexemType.SquareOpen, LexemType.SquareOpen))
				return null;

			Skip(2);

			var node = new NewListNode();
			node.Expressions = ParseInitExprBlock().ToList();
			if (node.Expressions.Count == 0)
				return null;

			if (!Peek(LexemType.SquareClose, LexemType.SquareClose))
				Error(ParserMessages.SymbolExpected, "]]");

			Skip(2);

			return node;
		}

		/// <summary>
		/// new_array_block                             = "[" init_expr_block "]"
		/// </summary>
		private NewArrayNode ParseNewArrayBlock()
		{
			if (!Check(LexemType.SquareOpen))
				return null;

			var node = new NewArrayNode();
			node.Expressions = ParseInitExprBlock().ToList();
			if (node.Expressions.Count == 0)
				return null;

			Ensure(LexemType.SquareClose, ParserMessages.SymbolExpected, "]");

			return node;
		}

		/// <summary>
		/// new_dict_block                              = "{" init_dict_expr_block "}"
		/// </summary>
		private NewDictionaryNode ParseNewDictBlock()
		{
			if (!Check(LexemType.CurlyOpen))
				return null;

			var node = new NewDictionaryNode();
			node.Expressions = ParseInitExprDictBlock().ToList();
			if (node.Expressions.Count == 0)
				return null;

			Ensure(LexemType.CurlyClose, ParserMessages.SymbolExpected, "}");

			return node;
		}

		/// <summary>
		/// init_expr_block                             = INDENT line_expr { NL line_expr } DEDENT
		/// </summary>
		private IEnumerable<NodeBase> ParseInitExprBlock()
		{
			if(Peek(LexemType.NewLine))
				Error(ParserMessages.InitializerIndentExprected);

			if (!Check(LexemType.Indent))
				yield break;

			yield return Ensure(ParseLineExpr, ParserMessages.InitExpressionExpected);

			while (!Check(LexemType.Dedent))
			{
				if(PeekAny(LexemType.CurlyClose, LexemType.SquareClose, LexemType.ParenClose))
					Error(ParserMessages.ClosingBraceNewLine);

				Ensure(LexemType.NewLine, ParserMessages.InitExpressionSeparatorExpected);
				yield return Ensure(ParseLineExpr, ParserMessages.InitExpressionExpected);
			}
		}

		/// <summary>
		/// init_expr_dict_block                        = INDENT init_dict_expr { NL init_dict_expr } DEDENT
		/// </summary>
		private IEnumerable<KeyValuePair<NodeBase, NodeBase>> ParseInitExprDictBlock()
		{
			if (!Check(LexemType.Indent))
				yield break;

			yield return ParseInitDictExpr();

			while (!Check(LexemType.Dedent))
			{
				Ensure(LexemType.NewLine, ParserMessages.InitExpressionSeparatorExpected);
				yield return ParseInitDictExpr();
			}
		}

		/// <summary>
		/// init_dict_expr                              = line_expr "=>" line_expr
		/// </summary>
		private KeyValuePair<NodeBase, NodeBase> ParseInitDictExpr()
		{
			var key = Ensure(ParseLineExpr, ParserMessages.DictionaryKeyExpected);
			Ensure(LexemType.FatArrow, ParserMessages.SymbolExpected, "=>");
			var value = Ensure(ParseLineExpr, ParserMessages.DictionaryValueExpected);

			return new KeyValuePair<NodeBase, NodeBase>(key, value);
		}

		/// <summary>
		/// new_object_block                            = type invoke_block_args
		/// </summary>
		private NewObjectNode ParseNewObjectBlock()
		{
			var type = Attempt(ParseType);
			if (type == null)
				return null;

			var args = ParseInvokeBlockArgs().ToList();
			if (args.Count == 0)
				return null;

			var node = new NewObjectNode();
			node.TypeSignature = type;
			node.Arguments = args;
			return node;
		}

		#endregion

		#region Block invocations

		/// <summary>
		/// invoke_block_expr                           = line_expr [ INDENT invoke_pass { NL invoke_pass } DEDENT ]
		/// </summary>
		private NodeBase ParseInvokeBlockExpr()
		{
			var expr = Attempt(ParseLineExpr);
			if (expr == null)
				return null;

			if (!Check(LexemType.Indent))
				return null;
			
			var pass = Attempt(ParseInvokePass);
			if (pass == null)
				return null;

			(pass.Expression as GetMemberNode).Expression = expr;
			expr = pass;

			while (!Check(LexemType.Dedent))
			{
				Ensure(LexemType.NewLine, ParserMessages.InvokePassSeparatorExpected);
				pass = Attempt(ParseInvokePass);
				if (pass == null)
					return expr;

				(pass.Expression as GetMemberNode).Expression = expr;
				expr = pass;
			}
			
			return expr;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private InvocationNode ParseInvokeBlockPassExpr()
		{
			var expr = Attempt(ParseLineExpr);
			if (expr == null)
				return null;

			var args = ParseInvokeBlockArgs().ToList();
			if (args.Count == 0)
				return null;

			return new InvocationNode {Expression = expr, Arguments = args};
		}
		
		/// <summary>
		/// invoke_pass                                 = "|>" identifier [ type_args ] ( invoke_block_args | invoke_line_args )
		/// </summary>
		private InvocationNode ParseInvokePass()
		{
			if (!Check(LexemType.PassRight))
				return null;

			var getter = new GetMemberNode();
			var invoker = new InvocationNode { Expression = getter };

			getter.MemberName = Ensure(LexemType.Identifier, ParserMessages.MemberNameExpected).Value;

			var hints = Attempt(ParseTypeArgs);
			if(hints != null)
				getter.TypeHints = hints;

			var lambda = Attempt(ParseLambdaLineExpr);
			if (lambda != null)
			{
				invoker.Arguments = new List<NodeBase> { lambda };
			}
			else
			{
				invoker.Arguments = ParseInvokeBlockArgs().ToList();
				if (invoker.Arguments.Count == 0)
					invoker.Arguments = ParseInvokeLineArgs().ToList();

				if (invoker.Arguments.Count == 0)
					Error(ParserMessages.ArgumentsExpected);
			}

			return invoker;
		}

		/// <summary>
		/// invoke_block_args                           = INDENT invoke_block_arg { NL invoke_block_arg } DEDENT
		/// </summary>
		private IEnumerable<NodeBase> ParseInvokeBlockArgs()
		{
			if (!Check(LexemType.Indent))
				yield break;

			var node = Attempt(ParseInvokeBlockArg);
			if (node == null)
				yield break;

			yield return node;

			while (!Check(LexemType.Dedent))
			{
				if(!IsStmtSeparator())
					Error("Argument passes must be separated by newlines!");

				node = Attempt(ParseInvokeBlockArg);
				if (node == null)
					yield break;

				yield return node;
			}
		}

		/// <summary>
		/// invoke_block_arg                            = "<|" ( ref_arg | expr )
		/// </summary>
		private NodeBase ParseInvokeBlockArg()
		{
			if (!Check(LexemType.PassLeft))
				return null;

			return Attempt(ParseRefArg)
				   ?? Ensure(ParseExpr, ParserMessages.ExpressionExpected);
		}
		
		/// <summary>
		/// invoke_line_args                            = { invoke_line_arg }
		/// </summary>
		private IEnumerable<NodeBase> ParseInvokeLineArgs()
		{
			while (true)
			{
				var curr = Attempt(ParseInvokeLineArg);
				if (curr == null)
					yield break;

				yield return curr;
			}
		}

		/// <summary>
		/// invoke_line_arg                             = ref_arg | get_expr
		/// </summary>
		private NodeBase ParseInvokeLineArg()
		{
			return Attempt(ParseRefArg)
				   ?? Attempt(ParseGetExpr);
		}

		/// <summary>
		/// ref_arg                                     = "ref" lvalue_expr | "(" "ref" lvalue_expr ")"
		/// </summary>
		private NodeBase ParseRefArg()
		{
			var paren = Check(LexemType.ParenOpen);

			if (!Check(LexemType.Ref))
				return null;

			var node = Ensure(ParseLvalueExpr, ParserMessages.RefLvalueExpected);
			(node as IPointerProvider).RefArgumentRequired = true;

			if (paren)
				Ensure(LexemType.ParenClose, ParserMessages.SymbolExpected, ')');

			return node;
		}

		#endregion

		#region Pattern matching

		/// <summary>
		/// match_block                                 = "match" line_expr "with" INDENT { match_stmt } DEDENT
		/// </summary>
		private MatchNode ParseMatchBlock()
		{
			if (!Check(LexemType.Match))
				return null;

			var node = new MatchNode { Expression = Ensure(ParseLineExpr, ParserMessages.MatchExpressionExpected) };

			Ensure(LexemType.With, ParserMessages.SymbolExpected, "with");
			Ensure(LexemType.Indent, ParserMessages.MatchIndentExpected);

			node.MatchStatements.Add(Ensure(ParseMatchStmt, ParserMessages.SymbolExpected, "case"));

			while (!Check(LexemType.Dedent))
			{
				Ensure(LexemType.NewLine, ParserMessages.NewlineSeparatorExpected);
				node.MatchStatements.Add(Ensure(ParseMatchStmt, ParserMessages.SymbolExpected, "case"));
			}

			return node;
		}

		/// <summary>
		/// match_stmt                                  = "case" match_rules [ "when" line_expr ] "then" block
		/// </summary>
		private MatchStatementNode ParseMatchStmt()
		{
			Ensure(LexemType.Case, ParserMessages.SymbolExpected, "case");

			var node = new MatchStatementNode { MatchRules = ParseMatchRules().ToList() };

			if (Check(LexemType.When))
				node.Condition = Ensure(ParseLineExpr, ParserMessages.WhenGuardExpressionExpected);

			Ensure(LexemType.Then, ParserMessages.SymbolExpected, "then");

			node.Expression = Ensure(ParseBlock, ParserMessages.CodeBlockExpected);

			return node;
		}

		/// <summary>
		/// match_rules                                 = match_rule { [ NL ] "|" match_rule }
		/// </summary>
		private IEnumerable<MatchRuleBase> ParseMatchRules()
		{
			yield return Ensure(ParseMatchRule, ParserMessages.MatchRuleExpected);

			while (true)
			{
				Check(LexemType.NewLine);
				if (!Check(LexemType.BitOr))
					yield break;

				yield return Ensure(ParseMatchRule, ParserMessages.MatchRuleExpected);
			}
		}

		/// <summary>
		/// match_rule                                  = rule_generic [ rule_keyvalue ]
		/// </summary>
		private MatchRuleBase ParseMatchRule()
		{
			var rule = ParseRuleGeneric();
			var keyValue = Attempt(ParseRuleKeyValue);

			if (keyValue == null)
				return rule;

			keyValue.KeyRule = rule;
			return keyValue;
		}

		#endregion

		#region Match rules

		/// <summary>
		/// rule_generic                                = rule_tuple | rule_array | rule_regex | rule_record | rule_type | rule_range | rule_literal | rule_name
		/// </summary>
		private MatchRuleBase ParseRuleGeneric()
		{
			return Attempt(ParseRuleTuple)
			       ?? Attempt(ParseRuleArray)
				   ?? Attempt(ParseRegex)
			       ?? Attempt(ParseRuleRecord)
			       ?? Attempt(ParseRuleType)
			       ?? Attempt(ParseRuleRange)
			       ?? Attempt(ParseRuleLiteral)
				   ?? Attempt(ParseRuleName) as MatchRuleBase;
		}

		/// <summary>
		/// rule_tuple                                  = "(" match_rule { ";" match_rule } ")"
		/// </summary>
		private MatchTupleRule ParseRuleTuple()
		{
			if (!Check(LexemType.ParenOpen))
				return null;

			var node = new MatchTupleRule();
			node.ElementRules.Add(Ensure(ParseMatchRule, ParserMessages.MatchRuleExpected));
			while(Check(LexemType.Semicolon))
				node.ElementRules.Add(Ensure(ParseMatchRule, ParserMessages.MatchRuleExpected));

			Ensure(LexemType.ParenClose, ParserMessages.SymbolExpected, ")");

			return node;
		}

		/// <summary>
		/// rule_array                                  = "[" [ rule_array_item { ";" rule_array_item } ] "]"
		/// </summary>
		private MatchRuleBase ParseRuleArray()
		{
			if (!Check(LexemType.SquareOpen))
				return null;

			var node = new MatchArrayRule();
			if (!Check(LexemType.SquareClose))
			{
				node.ElementRules.Add(Ensure(ParseRuleArrayItem, ParserMessages.MatchRuleExpected));

				while (Check(LexemType.Semicolon))
					node.ElementRules.Add(Ensure(ParseRuleArrayItem, ParserMessages.MatchRuleExpected));

				Ensure(LexemType.SquareClose, ParserMessages.SymbolExpected, "]");
			}

			return node;
		}

		/// <summary>
		/// rule_regex                                 = regex
		/// </summary>
		private MatchRegexNode ParseRegex()
		{
			if (!Peek(LexemType.Regex))
				return null;

			var raw = GetValue();
			var trailPos = raw.LastIndexOf('#');
			var value = raw.Substring(1, trailPos - 1);
			var mods = raw.Substring(trailPos + 1);
			return new MatchRegexNode {Value = value, Modifiers = mods};
		}

		/// <summary>
		/// rule_array_item                             = rule_subsequence | match_rule
		/// </summary>
		private MatchRuleBase ParseRuleArrayItem()
		{
			return Attempt(ParseRuleSubsequence)
			       ?? Attempt(ParseMatchRule);
		}

		/// <summary>
		/// rule_record                                 = identifier "(" rule_record_var { ";" rule_record_var } ")"
		/// </summary>
		private MatchRecordRule ParseRuleRecord()
		{
			if (!Peek(LexemType.Identifier, LexemType.ParenOpen))
				return null;

			var identifier = ParseType();
			Skip();

			var node = new MatchRecordRule { Identifier = identifier };
			node.FieldRules.Add(Ensure(ParseRuleRecordVar, ParserMessages.MatchRuleExpected));
			while(Check(LexemType.Semicolon))
				node.FieldRules.Add(Ensure(ParseRuleRecordVar, ParserMessages.MatchRuleExpected));

			Ensure(LexemType.ParenClose, ParserMessages.SymbolExpected, ")");

			return node;
		}

		/// <summary>
		/// rule_record_var                             = identifier "=" match_rule
		/// </summary>
		private MatchRecordField ParseRuleRecordVar()
		{
			if (!Peek(LexemType.Identifier, LexemType.Assign))
				return null;

			var identifier = ParseType();
			Skip();

			return new MatchRecordField
			{
				Name = identifier,
				Rule = Ensure(ParseMatchRule, ParserMessages.MatchRuleExpected)
			};
		}

		/// <summary>
		/// rule_type                                   = identifier "of" match_rule
		/// </summary>
		private MatchTypeRule ParseRuleType()
		{
			if (!Peek(LexemType.Identifier, LexemType.Of))
				return null;

			var identifier = ParseType();
			Skip();

			return new MatchTypeRule
			{
				Identifier = identifier,
				LabelRule = Ensure(ParseMatchRule, ParserMessages.MatchRuleExpected)
			};
		}

		/// <summary>
		/// rule_range                                  = rule_literal ".." rule_literal
		/// </summary>
		private MatchRangeRule ParseRuleRange()
		{
			var start = Attempt(ParseRuleLiteral);
			if (start == null)
				return null;

			if (!Check(LexemType.DoubleDot))
				return null;

			return new MatchRangeRule
			{
				RangeStartRule = start,
				RangeEndRule = Ensure(ParseRuleLiteral, ParserMessages.MatchRuleExpected)
			};
		}

		/// <summary>
		/// rule_literal                                = literal
		/// </summary>
		private MatchLiteralRule ParseRuleLiteral()
		{
			var literal = Attempt(ParseLiteral);
			if (literal == null)
				return null;

			return new MatchLiteralRule { Literal = (ILiteralNode)literal };
		}

		/// <summary>
		/// rule_name                                   = identifier [ ":" type ]
		/// </summary>
		private MatchNameRule ParseRuleName()
		{
			if (!Peek(LexemType.Identifier))
				return null;

			var node = new MatchNameRule { Name = GetValue() };
			if (Check(LexemType.Colon))
				node.Type = Ensure(ParseType, ParserMessages.TypeSignatureExpected);

			return node;
		}

		/// <summary>
		/// rule_subsequence                            = "..." identifier
		/// </summary>
		private MatchNameRule ParseRuleSubsequence()
		{
			if (!Check(LexemType.Ellipsis))
				return null;

			return new MatchNameRule
			{
				Name = GetValue(),
				IsArraySubsequence = true
			};
		}

		/// <summary>
		/// rule_keyvalue                               = "=>" rule_generic
		/// </summary>
		private MatchKeyValueRule ParseRuleKeyValue()
		{
			if (!Check(LexemType.FatArrow))
				return null;

			// key is substituted in match_rule
			return new MatchKeyValueRule { ValueRule = Ensure(ParseRuleGeneric, ParserMessages.MatchRuleExpected) };
		}

		#endregion

		#region Line expressions

		/// <summary>
		/// line_stmt                                   = set_stmt | line_expr
		/// </summary>
		private NodeBase ParseLineStmt()
		{
			return Attempt(ParseSetStmt)
				   ?? Attempt(ParseLineExpr);
		}

		/// <summary>
		/// line_expr                                   = if_line | while_line | for_line | using_line | throw_stmt | new_object_line | typeop_expr | line_typecheck_expr
		/// </summary>
		private NodeBase ParseLineExpr()
		{
			return Attempt(ParseIfLine)
				   ?? Attempt(ParseWhileLine)
				   ?? Attempt(ParseForLine)
				   ?? Attempt(ParseUsingLine)
				   ?? Attempt(ParseThrowStmt)
				   ?? Attempt(ParseNewObjectExpr)
				   ?? Attempt(ParseTypeopExpr)
				   ?? Attempt(ParseLineTypecheckExpr);
		}

		/// <summary>
		/// typeop_expr                                 = default_expr | typeof_expr
		/// </summary>
		private NodeBase ParseTypeopExpr()
		{
			return Attempt(ParseDefaultExpr)
				   ?? Attempt(ParseTypeofExpr) as NodeBase;
		}

		/// <summary>
		/// default_expr                                = "default" type
		/// </summary>
		private DefaultOperatorNode ParseDefaultExpr()
		{
			if (!Check(LexemType.Default))
				return null;

			return new DefaultOperatorNode { TypeSignature = Ensure(ParseType, ParserMessages.TypeSignatureExpected) };
		}

		/// <summary>
		/// typeof_expr                                 = "typeof" type
		/// </summary>
		private TypeofOperatorNode ParseTypeofExpr()
		{
			if (!Check(LexemType.Typeof))
				return null;

			return new TypeofOperatorNode {TypeSignature = Ensure(ParseType, ParserMessages.TypeSignatureExpected) };
		}

		/// <summary>
		/// line_typecheck_expr                         = line_op_expr [ typecheck_op_expr ]
		/// </summary>
		private NodeBase ParseLineTypecheckExpr()
		{
			var node = Attempt(ParseLineOpExpr);
			if (node == null)
				return null;

			var typeop = Attempt(ParseTypecheckOpExpr);

			var cast = typeop as CastOperatorNode;
			if (cast != null)
			{
				cast.Expression = node;
				return cast;
			}

			var check = typeop as IsOperatorNode;
			if (check != null)
			{
				check.Expression = node;
				return check;
			}

			return node;
		}

		/// <summary>
		/// line_op_expr                                = [ unary_op ] line_base_expr { binary_op line_base_expr }
		/// </summary>
		private NodeBase ParseLineOpExpr()
		{
			// parser magic
			return ProcessOperator(ParseLineBaseExpr);
		}

		/// <summary>
		/// typecheck_op_expr                           = "as" type | "is" type
		/// </summary>
		private NodeBase ParseTypecheckOpExpr()
		{
			if (Check(LexemType.Is))
				return new IsOperatorNode {TypeSignature = Ensure(ParseType, ParserMessages.TypeSignatureExpected)};

			if (Check(LexemType.As))
				return new CastOperatorNode { TypeSignature = Ensure(ParseType, ParserMessages.TypeSignatureExpected) };

			return null;
		}

		/// <summary>
		/// line_base_expr                              = line_invoke_base_expr | get_expr
		/// </summary>
		private NodeBase ParseLineBaseExpr()
		{
			return Attempt(ParseLineInvokeBaseExpr)
				   ?? Attempt(ParseGetExpr);
		}

		/// <summary>
		/// line_invoke_base_expr                       = get_expr [ invoke_line_args ]
		/// </summary>
		private NodeBase ParseLineInvokeBaseExpr()
		{
			var expr = Attempt(ParseGetExpr);
			if (expr == null)
				return null;

			var args = ParseInvokeLineArgs().ToList();
			if (args.Count == 0)
				return expr;

			var node = new InvocationNode();
			node.Expression = expr;
			node.Arguments = args;
			return node;
		}


		/// <summary>
		/// atom                                        = literal | get_id_expr | get_stmbr_expr | new_collection_expr | paren_expr
		/// </summary>
		private NodeBase ParseAtom()
		{
			return Attempt(ParseLiteral)
				   ?? Attempt(ParseGetStmbrExpr)
				   ?? Attempt(ParseGetIdExpr)
				   ?? Attempt(ParseNewCollectionExpr)
				   ?? Attempt(ParseParenExpr);
		}

		/// <summary>
		/// paren_expr                                  = "(" ( lambda_line_expr | line_expr ) ")"
		/// </summary>
		private NodeBase ParseParenExpr()
		{
			if (!Check(LexemType.ParenOpen))
				return null;

			var expr = Attempt(ParseLambdaLineExpr)
					   ?? Ensure(ParseLineStmt, ParserMessages.ExpressionExpected);

			if (expr != null)
			{
				if (Peek(LexemType.Colon))
					return null;

				Ensure(LexemType.ParenClose, ParserMessages.SymbolExpected, ')');
			}

			return expr;
		}

		/// <summary>
		/// lambda_line_expr                            = [ fun_args ] "->" line_expr
		/// </summary>
		private LambdaNode ParseLambdaLineExpr()
		{
			var node = new LambdaNode();
			node.Arguments = Attempt(() => ParseFunArgs()) ?? new List<FunctionArgument>();
			
			if (!Check(LexemType.Arrow))
				return null;

			node.Body.Add(Ensure(ParseLineStmt, ParserMessages.FunctionBodyExpected));
			return node;
		}

		#endregion

		#region Line control structures

		/// <summary>
		/// if_line                                     = if_header line_stmt [ "else" line_stmt ]
		/// </summary>
		private IfNode ParseIfLine()
		{
			var node = Attempt(ParseIfHeader);
			if (node == null)
				return null;

			node.TrueAction.Add(Ensure(ParseLineStmt, ParserMessages.ConditionExpressionExpected));
			if ( Check(LexemType.Else))
				node.FalseAction = new CodeBlockNode { Ensure(ParseLineStmt, ParserMessages.ExpressionExpected) };

			return node;
		}

		/// <summary>
		/// while_line                                  = while_header line_stmt
		/// </summary>
		private WhileNode ParseWhileLine()
		{
			var node = Attempt(ParseWhileHeader);
			if (node == null)
				return null;

			node.Body.Add(Ensure(ParseLineStmt, ParserMessages.LoopExpressionExpected));
			return node;
		}

		/// <summary>
		/// for_line                                    = for_header line_stmt
		/// </summary>
		private ForeachNode ParseForLine()
		{
			var node = Attempt(ParseForHeader);
			if (node == null)
				return null;

			node.Body.Add(Ensure(ParseLineStmt, ParserMessages.LoopExpressionExpected));
			return node;
		}

		/// <summary>
		/// using_line                                  = using_header line_stmt
		/// </summary>
		private UsingNode ParseUsingLine()
		{
			var node = Attempt(ParseUsingHeader);
			if (node == null)
				return null;

			node.Body.Add(Ensure(ParseLineStmt, ParserMessages.UsingExpressionExpected));
			return node;
		}

		/// <summary>
		/// throw_stmt                                  = "throw" [ line_expr ]
		/// </summary>
		private ThrowNode ParseThrowStmt()
		{
			if (!Check(LexemType.Throw))
				return null;

			var node = new ThrowNode();
			node.Expression = Attempt(ParseLineExpr);
			return node;
		}
		
		#endregion

		#region Line initializers

		/// <summary>
		/// new_line_expr                               = "new" ( new_objarray_line | new_object_block )
		/// </summary>
		private NodeBase ParseNewObjectExpr()
		{
			if (!Check(LexemType.New))
				return null;

			return Attempt(ParseNewObjArrayLine)
				   ?? Attempt(ParseNewObjectLine) as NodeBase;
		}

		/// <summary>
		/// new_collection_expr                        = "new" ( new_tuple_line | new_list_line | new_array_line | new_dict_line )
		/// </summary>
		private NodeBase ParseNewCollectionExpr()
		{
			if (!Check(LexemType.New))
				return null;

			return Attempt(ParseNewTupleLine)
				   ?? Attempt(ParseNewListLine)
				   ?? Attempt(ParseNewArrayLine)
				   ?? Attempt(ParseNewDictLine) as NodeBase;
		}

		/// <summary>
		/// new_tuple_line                             = "(" init_expr_line ")"
		/// </summary>
		private NewTupleNode ParseNewTupleLine()
		{
			// todo: revise grammar, possibly eliminating the unit literal
			if(Check(LexemType.Unit))
				Error(ParserMessages.TupleItem);

			if (!Check(LexemType.ParenOpen))
				return null;

			var node = new NewTupleNode();
			node.Expressions = ParseInitExprLine().ToList();

			if (node.Expressions.Count == 0)
				Error(ParserMessages.TupleItem);

			Ensure(LexemType.ParenClose, ParserMessages.SymbolExpected, ")");

			return node;
		}

		/// <summary>
		/// new_list_line                               = "[" "[" init_expr_block "]" "]"
		/// </summary>
		private NewListNode ParseNewListLine()
		{
			if (!Peek(LexemType.SquareOpen, LexemType.SquareOpen))
				return null;

			Skip(2);

			var node = new NewListNode();
			node.Expressions = ParseInitExprLine().ToList();

			if (node.Expressions.Count == 0)
				Error(ParserMessages.ListItem);

			if (!Peek(LexemType.SquareClose, LexemType.SquareClose))
				Error(ParserMessages.SymbolExpected, "]]");

			Skip(2);

			return node;
		}

		/// <summary>
		/// new_array_line                              = "[" init_expr_block "]"
		/// </summary>
		private NewArrayNode ParseNewArrayLine()
		{
			if (!Check(LexemType.SquareOpen))
				return null;

			var node = new NewArrayNode();
			node.Expressions = ParseInitExprLine().ToList();

			if (node.Expressions.Count == 0)
				Error(ParserMessages.ArrayItem);

			Ensure(LexemType.SquareClose, ParserMessages.SymbolExpected, "]");

			return node;
		}

		/// <summary>
		/// new_dict_line                               = "{" init_dict_expr_block "}"
		/// </summary>
		private NewDictionaryNode ParseNewDictLine()
		{
			if (!Check(LexemType.CurlyOpen))
				return null;

			var node = new NewDictionaryNode();
			node.Expressions = ParseInitExprDictLine().ToList();

			if (node.Expressions.Count == 0)
				Error(ParserMessages.DictionaryItem);

			Ensure(LexemType.CurlyClose, ParserMessages.SymbolExpected, "}");

			return node;
		}

		/// <summary>
		/// init_expr_line                              = line_expr { ";" line_expr }
		/// </summary>
		private IEnumerable<NodeBase> ParseInitExprLine()
		{
			var node = Attempt(ParseLineExpr);
			if(node == null)
				yield break;

			yield return node;
			while (Check(LexemType.Semicolon))
				yield return Ensure(ParseLineExpr, ParserMessages.ExpressionExpected);
		}

		/// <summary>
		/// init_expr_dict_line                         = init_dict_expr { ";" init_dict_expr }
		/// </summary>
		private IEnumerable<KeyValuePair<NodeBase, NodeBase>> ParseInitExprDictLine()
		{
			if (Check(LexemType.CurlyClose))
				yield break;

			yield return ParseInitDictExpr();

			while (Check(LexemType.Semicolon))
				yield return ParseInitDictExpr();
		}

		/// <summary>
		/// new_objarray_line                          = type "[" line_expr "]"
		/// </summary>
		private NewObjectArrayNode ParseNewObjArrayLine()
		{
			var type = Attempt(ParseType);
			if (type == null)
				return null;

			if (!Check(LexemType.SquareOpen))
				return null;

			var node = new NewObjectArrayNode();
			node.TypeSignature = type;
			node.Size = Ensure(ParseLineExpr, ParserMessages.ExpressionExpected);

			Ensure(LexemType.SquareClose, ParserMessages.SymbolExpected, "]");

			return node;
		}

		/// <summary>
		/// new_object_line                             = type invoke_line_args
		/// </summary>
		private NewObjectNode ParseNewObjectLine()
		{
			var type = Attempt(ParseType);
			if (type == null)
				return null;

			var args = ParseInvokeLineArgs().ToList();
			if (args.Count == 0)
				return null;

			var node = new NewObjectNode();
			node.TypeSignature = type;
			node.Arguments = args;
			return node;
		}

		#endregion

		#region Literals

		/// <summary>
		/// literal                                     = unit | null | bool | int | long | float | double | decimal | char | string
		/// </summary>
		private NodeBase ParseLiteral()
		{
			return Attempt(ParseUnit)
				   ?? Attempt(ParseNull)
				   ?? Attempt(ParseBool)
				   ?? Attempt(ParseInt)
				   ?? Attempt(ParseLong)
				   ?? Attempt(ParseFloat)
				   ?? Attempt(ParseDouble)
				   ?? Attempt(ParseDecimal)
				   ?? Attempt(ParseChar)
				   ?? Attempt(ParseString) as NodeBase;
		}

		private UnitNode ParseUnit()
		{
			return Check(LexemType.Unit) ? new UnitNode() : null;
		}

		private NullNode ParseNull()
		{
			return Check(LexemType.Null) ? new NullNode() : null;
		}

		private BooleanNode ParseBool()
		{
			if(Check(LexemType.True))
				return new BooleanNode(true);

			if (Check(LexemType.False))
				return new BooleanNode();

			return null;
		}

		private StringNode ParseString()
		{
			if (!Peek(LexemType.String))
				return null;

			return new StringNode(GetValue());
		}

		private IntNode ParseInt()
		{
			if (!Peek(LexemType.Int))
				return null;

			var value = GetValue();
			try
			{
				return new IntNode(int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
			}
			catch
			{
				Error(ParserMessages.InvalidInteger, value);
				return null;
			}
		}

		private LongNode ParseLong()
		{
			if (!Peek(LexemType.Long))
				return null;

			var value = GetValue();
			try
			{
				return new LongNode(long.Parse(value.Substring(0, value.Length - 1), NumberStyles.Integer, CultureInfo.InvariantCulture));
			}
			catch
			{
				Error(ParserMessages.InvalidLong, value);
				return null;
			}
		}

		private FloatNode ParseFloat()
		{
			if (!Peek(LexemType.Float))
				return null;

			var value = GetValue();
			try
			{
				return new FloatNode(float.Parse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture));
			}
			catch
			{
				Error(ParserMessages.InvalidFloat, value);
				return null;
			}
		}
 
		private DoubleNode ParseDouble()
		{
			if (!Peek(LexemType.Double))
				return null;

			var value = GetValue();
			try
			{
				return new DoubleNode(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			}
			catch
			{
				Error(ParserMessages.InvalidDouble, value);
				return null;
			}
		}

		private DecimalNode ParseDecimal()
		{
			if (!Peek(LexemType.Decimal))
				return null;

			var value = GetValue();
			try
			{
				return new DecimalNode(decimal.Parse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture));
			}
			catch
			{
				Error(ParserMessages.InvalidDecimal, value);
				return null;
			}
		}

		private CharNode ParseChar()
		{
			return Peek(LexemType.Char) ? new CharNode(GetValue()[0]) : null;
		}

		#endregion
	}
}
