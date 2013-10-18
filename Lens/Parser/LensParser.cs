using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.Compiler;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Expressions;

namespace Lens.Parser
{
	internal partial class LensParser
	{
		public List<NodeBase> Nodes { get; private set; }

		private Lexem[] Lexems;
		private int LexemId;

		public LensParser(IEnumerable<Lexem> lexems)
		{
			Lexems = lexems.ToArray();

			Nodes = parseMain().ToList();
		}

		#region Globals

		/// <summary>
		/// main                                        = { stmt } EOF
		/// </summary>
		private IEnumerable<NodeBase> parseMain()
		{
			while (!peek(LexemType.EOF))
				yield return parseStmt();

			skip();
		}

		/// <summary>
		/// stmt                                        = using | record_def | type_def | fun_def | local_stmt
		/// </summary>
		private NodeBase parseStmt()
		{
			return attempt(parseUsing)
			       ?? attempt(parseRecordDef)
			       ?? attempt(parseTypeDef)
			       ?? attempt(parseFunDef)
			       ?? ensure(parseLocalStmt, "Unknown kind of statement!");
		}

		#endregion

		#region Namespace & type signatures

		/// <summary>
		/// namespace                                   = identifier { "." identifier }
		/// </summary>
		private TypeSignature parseNamespace()
		{
			return bind(() =>
				{
					if (!peek(LexemType.Identifier))
						return null;

					var identifier = getValue();
					if (!peek(LexemType.Dot))
						return new TypeSignature(identifier);

					var sb = new StringBuilder(identifier);
					while (check(LexemType.Dot))
					{
						identifier = ensure(LexemType.Identifier, "An identifier is expected!").Value;
						sb.Append(".");
						sb.Append(identifier);
					}

					return new TypeSignature(sb.ToString());
				}
			);
		}

		/// <summary>
		/// type                                        = namespace [ type_args ] { "[]" }
		/// </summary>
		private TypeSignature parseType()
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Structures

		/// <summary>
		/// using                                       = "using" namespace NL
		/// </summary>
		private UsingNode parseUsing()
		{
			if (!check(LexemType.Using))
				return null;

			var nsp = ensure(parseNamespace, "A namespace is expected!");
			ensure(LexemType.NewLine, "A using statement should end with a newline!");

			return new UsingNode {Namespace = nsp.FullSignature};
		}

		/// <summary>
		/// record_def                                  = "record" identifier INDENT record_stmt { record_stmt } DEDENT
		/// </summary>
		private RecordDefinitionNode parseRecordDef()
		{
			if (!check(LexemType.Record))
				return null;

			var node = new RecordDefinitionNode();

			node.Name = ensure(LexemType.Identifier, "Record name must be an identifier!").Value;
			ensure(LexemType.Indent, "Record body must be indented block!");
			
			var field = bind(parseRecordStmt);
			node.Entries.Add(field);

			while (!check(LexemType.Dedent))
			{
				field = bind(parseRecordStmt);
				node.Entries.Add(field);
			}

			return node;
		}

		/// <summary>
		/// record_stmt                                 = identifier ":" type NL
		/// </summary>
		private RecordField parseRecordStmt()
		{
			var node = new RecordField();

			node.Name = ensure(LexemType.Identifier, "Record field name must be an identifier!").Value;
			ensure(LexemType.Colon, "Colon is expected!");
			node.Type = ensure(parseType, "Record field type specified is expected!");

			return node;
		}

		/// <summary>
		/// type_def                                    = "type" identifier INDENT type_stmt { type_stmt } DEDENT
		/// </summary>
		private TypeDefinitionNode parseTypeDef()
		{
			if (!check(LexemType.Type))
				return null;

			var node = new TypeDefinitionNode();

			node.Name = ensure(LexemType.Identifier, "Type name must be an identifier!").Value;
			ensure(LexemType.Indent, "Type body must be indented block!");

			var field = bind(parseTypeStmt);
			node.Entries.Add(field);

			while (!check(LexemType.Dedent))
			{
				field = bind(parseTypeStmt);
				node.Entries.Add(field);
			}

			return node;
		}

		/// <summary>
		/// type_stmt                                   = identifier [ "of" type ] NL
		/// </summary>
		private TypeLabel parseTypeStmt()
		{
			var node = new TypeLabel();

			node.Name = ensure(LexemType.Identifier, "Type label name must be an identifier!").Value;
			if (check(LexemType.Of))
				node.TagType = ensure(parseType, "Label type is expected!");

			return node;
		}

		/// <summary>
		/// fun_def                                     = [ "pure" ] "fun" identifier [ ":" type ] fun_args "->" block
		/// </summary>
		private FunctionNode parseFunDef()
		{
			var node = new FunctionNode();
			node.IsPure = check(LexemType.Pure);

			if (!check(LexemType.Fun))
			{
				if (node.IsPure)
					error("Function definition is expected!");
				else
					return null;
			}

			node.Name = ensure(LexemType.Identifier, "Function name must be an identifier!").Value;
			if (check(LexemType.Colon))
				node.ReturnTypeSignature = ensure(parseType, "Function return type is expected!");

			node.Arguments = parseFunArgs();
			ensure(LexemType.Arrow, "Arrow is expected!");
			node.Body = ensure(parseBlock, "Function body is expected!");

			return node;
		}

		/// <summary>
		/// fun_args                                    = fun_single_arg | fun_many_args
		/// </summary>
		private List<FunctionArgument> parseFunArgs()
		{
			var single = attempt(parseFunSingleArg);
			if (single != null)
				return new List<FunctionArgument> {single};

			var many = parseFunManyArgs().ToList();
			if (many.Count > 0)
				return many;

			return null;
		}

		/// <summary>
		/// fun_arg                                     = identifier ":" [ "ref" ] type
		/// </summary>
		private FunctionArgument parseFunSingleArg()
		{
			if (!peek(LexemType.Identifier))
				return null;

			var node = new FunctionArgument();
			node.Name = getValue();
			ensure(LexemType.Colon, "Colon is expected!");
			node.IsRefArgument = check(LexemType.Ref);
			node.TypeSignature = ensure(parseType, "Argument type is expected!");

			return node;
		}

		/// <summary>
		/// fun_arg_list                                = "(" { fun_single_arg } ")"
		/// </summary>
		private IEnumerable<FunctionArgument> parseFunManyArgs()
		{
			if (!check(LexemType.ParenOpen))
				yield break;

			while(!check(LexemType.ParenClose))
				yield return ensure(parseFunSingleArg, "A function argument is expected!");
		}

		#endregion

		#region Blocks

		/// <summary>
		/// block                                       = local_stmt_list | local_stmt
		/// </summary>
		private CodeBlockNode parseBlock()
		{
			var many = parseLocalStmtList().ToList();
			if (many.Count > 0)
				return new CodeBlockNode { Statements = many };

			var single = parseLetStmt();
			if (single != null)
				return new CodeBlockNode { single };

			return null;
		}

		/// <summary>
		/// local_stmt_list                             = INDENT local_stmt { NL local_stmt } DEDENT
		/// </summary>
		private IEnumerable<NodeBase> parseLocalStmtList()
		{
			if (!check(LexemType.Indent))
				yield break;

			yield return ensure(parseLocalStmt, "An expression is expected!");

			while (!check(LexemType.Dedent))
			{
				ensure(LexemType.NewLine, "Newline is expected!");
				yield return ensure(parseLocalStmt, "An expression is expected!");
			}
		}

		/// <summary>
		/// local_stmt                                  = name_def_stmt | set_stmt | expr
		/// </summary>
		private NodeBase parseLocalStmt()
		{
			return attempt(parseNameDefStmt)
			       ?? attempt(parseSetStmt)
			       ?? attempt(parseExpr);
		}

		#endregion

		#region Let & var

		/// <summary>
		/// name_def_stmt                               = var_stmt | let_stmt
		/// </summary>
		private NameDeclarationNodeBase parseNameDefStmt()
		{
			return attempt(parseVarStmt)
				   ?? (NameDeclarationNodeBase)attempt(parseLetStmt);
		}

		/// <summary>
		/// var_stmt                                    = "var" identifier ( "=" expr | ":" type )
		/// </summary>
		private VarNode parseVarStmt()
		{
			if (!check(LexemType.Var))
				return null;

			var node = new VarNode();
			node.Name = ensure(LexemType.Identifier, "Variable name must be an identifier!").Value;
			if (check(LexemType.Colon))
				node.Type = ensure(parseType, "Variable type is expected!");
			else if(check(LexemType.Equal))
				node.Value = ensure(parseExpr, "Initializer expression is expected!");
			else
				error("Initializer expresion or type signature is expected!");

			return node;
		}

		/// <summary>
		/// let_stmt                                    = "let" identifier "=" expr
		/// </summary>
		private LetNode parseLetStmt()
		{
			if (!check(LexemType.Let))
				return null;

			var node = new LetNode();
			node.Name = ensure(LexemType.Identifier, "Variable name must be an identifier!").Value;
			ensure(LexemType.Equal, "Assignment sign is expected!");
			node.Value = ensure(parseExpr, "Initializer expression is expected!");

			return node;
		}

		#endregion

		#region Assignment

		/// <summary>
		/// set_stmt                                    = set_id_stmt | set_stmbr_stmt | set_any_stmt
		/// </summary>
		private NodeBase parseSetStmt()
		{
			return attempt(parseSetIdStmt)
			       ?? attempt(parseSetStmbrStmt)
			       ?? attempt(parseSetAnyStmt);
		}

		/// <summary>
		/// set_id_stmt                                 = identifier "=" expr
		/// </summary>
		private SetIdentifierNode parseSetIdStmt()
		{
			if (!peek(LexemType.Identifier, LexemType.Equal))
				return null;

			var node = new SetIdentifierNode();
			node.Identifier = getValue();
			skip();
			node.Value = ensure(parseExpr, "Expression is expected!");

			return node;
		}

		/// <summary>
		/// set_stmbr_stmt <SetMemberNode>              = type "::" identifier "=" expr
		/// </summary>
		private SetMemberNode parseSetStmbrStmt()
		{
			var type = attempt(parseType);
			if (type == null)
				return null;

			if (!check(LexemType.DoubleСolon))
				return null;

			var node = new SetMemberNode();
			node.StaticType = type;
			node.MemberName = ensure(LexemType.Identifier, "Member name is expected!").Value;

			if (!check(LexemType.Equal))
				return null;

			node.Value = ensure(parseExpr, "Expression is expected!");

			return node;
		}

		/// <summary>
		/// set_any_stmt                                = lvalue_expr "=" expr
		/// </summary>
		private NodeBase parseSetAnyStmt()
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Lvalues
		#endregion

		#region Accessors
		#endregion

		#region Expression root

		/// <summary>
		/// expr                                        = block_expr | line_expr
		/// </summary>
		private NodeBase parseExpr()
		{
			return attempt(parseBlockExpr)
			       ?? attempt(parseLineExpr);
		}

		#endregion

		#region Block control structures

		/// <summary>
		/// block_expr                                  = if_expr | while_expr | for_expr | try_stmt | new_block_expr | invoke_block_expr | lambda_block_expr
		/// </summary>
		private NodeBase parseBlockExpr()
		{
			return attempt(parseIfExpr)
			       ?? attempt(parseWhileExpr)
			       ?? attempt(parseForExpr)
			       ?? attempt(parseTryStmt)
			       ?? attempt(parseNewBlockExpr)
			       ?? attempt(parseInvokeBlockExpr)
			       ?? attempt(parseLambdaBlockExpr);
		}

		/// <summary>
		/// if_block                                    = if_header block [ "else" block ]
		/// </summary>
		private IfNode parseIfExpr()
		{
			var node = attempt(parseIfHeader);
			if (node == null)
				return null;

			node.TrueAction = ensure(parseBlock, "Condition block is expected!");
			if (check(LexemType.Else))
				node.FalseAction = ensure(parseBlock, "Code block is expected!");

			return node;
		}

		/// <summary>
		/// while_block                                 = while_header block
		/// </summary>
		private WhileNode parseWhileExpr()
		{
			var node = attempt(parseWhileHeader);
			if (node == null)
				return null;

			node.Body = ensure(parseBlock, "Loop body block is expected!");
			return node;
		}

		/// <summary>
		/// for_block                                   = for_header block
		/// </summary>
		private ForeachNode parseForExpr()
		{
			var node = attempt(parseForHeader);
			if (node == null)
				return null;

			node.Body = ensure(parseBlock, "Loop body block is expected!");
			return node;
		}

		/// <summary>
		/// try_stmt                                    = "try" block catch_stmt_list [ finally_stmt ]
		/// </summary>
		private TryNode parseTryStmt()
		{
			if (!check(LexemType.Try))
				return null;

			var node = new TryNode();
			node.Code = ensure(parseBlock, "Try block is expected!");
			node.CatchClauses = parseCatchStmtList().ToList();

			if(node.CatchClauses.Count == 0)
				error("Catch clause is expected!");

			node.Finally = attempt(parseFinallyStmt);

			return node;
		}

		/// <summary>
		/// catch_stmt_list                             = catch_stmt { catch_stmt }
		/// </summary>
		private IEnumerable<CatchNode> parseCatchStmtList()
		{
			while (peek(LexemType.Catch))
				yield return parseCatchStmt();
		}

		/// <summary>
		/// catch_stmt                                  = "catch" [ identifier ":" type ] block
		/// </summary>
		private CatchNode parseCatchStmt()
		{
			if (!check(LexemType.Catch))
				return null;

			var node = new CatchNode();
			if (peek(LexemType.Identifier))
			{
				node.ExceptionVariable = getValue();
				ensure(LexemType.Colon, "Colon is expected!");
				node.ExceptionType = ensure(parseType, "Exception type is expected!");
			}

			node.Code = ensure(parseBlock, "Exception handler code block is expected!");
			return node;
		}

		/// <summary>
		/// finally_stmt                                = "finally" block
		/// </summary>
		private CodeBlockNode parseFinallyStmt()
		{
			if (!check(LexemType.Finally))
				return null;

			return parseBlock();
		}

		#endregion

		#region Headers

		/// <summary>
		/// if_header                                   = "if" line_expr "then"
		/// </summary>
		private IfNode parseIfHeader()
		{
			if (!check(LexemType.If))
				return null;

			var node = new IfNode();
			node.Condition = ensure(parseLineExpr, "Condition is expected!");
			ensure(LexemType.Then, "Then keyword is expected!");

			return node;
		}

		/// <summary>
		/// while_header                                = "while" line_expr "do"
		/// </summary>
		private WhileNode parseWhileHeader()
		{
			if (!check(LexemType.While))
				return null;

			var node = new WhileNode();
			node.Condition = ensure(parseLineExpr, "Condition is expected!");
			ensure(LexemType.Do, "Do keyword is expected!");

			return node;
		}

		/// <summary>
		/// for_block                                   = "for" identifier "in" line_expr [ ".." line_expr ] "do"
		/// </summary>
		private ForeachNode parseForHeader()
		{
			if (!check(LexemType.For))
				return null;

			var node = new ForeachNode();
			node.VariableName = ensure(LexemType.Identifier, "Variable name is expected!").Value;
			ensure(LexemType.In, "In keyword is expected!");

			var iter = ensure(parseLineExpr, "Sequence expression is expected!");
			if (check(LexemType.DoubleDot))
			{
				node.RangeStart = iter;
				node.RangeEnd = ensure(parseLineExpr, "Range end expression is expected!");
			}
			else
			{
				node.IterableExpression = iter;
			}

			ensure(LexemType.Do, "Do keyword is expected!");
			return node;
		}

		#endregion
	}
}
