using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Lens.Compiler;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Expressions;
using Lens.SyntaxTree.Literals;

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
			return attempt(parseIfBlock)
			       ?? attempt(parseWhileBlock)
			       ?? attempt(parseForBlock)
			       ?? attempt(parseTryStmt)
			       ?? attempt(parseNewBlockExpr)
			       ?? attempt(parseInvokeBlockExpr)
			       ?? attempt(parseLambdaBlockExpr);
		}

		/// <summary>
		/// if_block                                    = if_header block [ "else" block ]
		/// </summary>
		private IfNode parseIfBlock()
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
		private WhileNode parseWhileBlock()
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
		private ForeachNode parseForBlock()
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

		/// <summary>
		/// lambda_block_expr                           = [ fun_args ] "->" block
		/// </summary>
		private LambdaNode parseLambdaBlockExpr()
		{
			var node = new LambdaNode();
			node.Arguments = parseFunArgs();

			if (!check(LexemType.Arrow))
				return null;

			node.Body = ensure(parseBlock, "Function body is expected!");
			return node;
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

		#region Block initializers

		/// <summary>
		/// new_block_expr                              = "new" new_tuple_block | new_array_block | new_list_block | new_dict_block | new_object_block
		/// </summary>
		private NodeBase parseNewBlockExpr()
		{
			if (!check(LexemType.New))
				return null;

			return attempt(parseNewTupleBlock)
			       ?? attempt(parseNewListBlock)
			       ?? attempt(parseNewArrayBlock)
			       ?? attempt(parseNewDictBlock)
			       ?? attempt(parseNewObjectBlock) as NodeBase;
		}

		/// <summary>
		/// new_tuple_block                             = "(" init_expr_block ")"
		/// </summary>
		private NewTupleNode parseNewTupleBlock()
		{
			if (!check(LexemType.ParenOpen))
				return null;

			var node = new NewTupleNode();
			node.Expressions = parseInitExprBlock().ToList();
			if(node.Expressions.Count == 0)
				error("A tuple must contain at least one item!");

			ensure(LexemType.ParenClose, "Unmatched brace!");

			return node;
		}

		/// <summary>
		/// new_list_block                              = "[[" init_expr_block "]]"
		/// </summary>
		private NewListNode parseNewListBlock()
		{
			if (!check(LexemType.DoubleSquareOpen))
				return null;

			var node = new NewListNode();
			node.Expressions = parseInitExprBlock().ToList();
			if (node.Expressions.Count == 0)
				error("A list must contain at least one item!");

			ensure(LexemType.DoubleSquareClose, "Unmatched brace!");

			return node;
		}

		/// <summary>
		/// new_array_block                             = "[" init_expr_block "]"
		/// </summary>
		private NewArrayNode parseNewArrayBlock()
		{
			if (!check(LexemType.SquareOpen))
				return null;

			var node = new NewArrayNode();
			node.Expressions = parseInitExprBlock().ToList();
			if (node.Expressions.Count == 0)
				error("An array must contain at least one item!");

			ensure(LexemType.SquareClose, "Unmatched brace!");

			return node;
		}

		/// <summary>
		/// new_dict_block                              = "{" init_dict_expr_block "}"
		/// </summary>
		private NewDictionaryNode parseNewDictBlock()
		{
			if (!check(LexemType.CurlyOpen))
				return null;

			var node = new NewDictionaryNode();
			node.Expressions = parseInitExprDictBlock().ToList();
			if (node.Expressions.Count == 0)
				error("A dictionary must contain at least one item!");

			ensure(LexemType.CurlyClose, "Unmatched brace!");

			return node;
		}

		/// <summary>
		/// init_expr_block                             = INDENT line_expr { NL line_expr } DEDENT
		/// </summary>
		private IEnumerable<NodeBase> parseInitExprBlock()
		{
			if (!check(LexemType.Indent))
				yield break;

			yield return ensure(parseLineExpr, "Initializer expression expected!");

			while (!check(LexemType.Dedent))
			{
				ensure(LexemType.NewLine, "Initializer expressions must be separated by a newline!");
				yield return ensure(parseLineExpr, "Initializer expression expected!");
			}
		}

		/// <summary>
		/// init_expr_dict_block                        = INDENT init_dict_expr { NL init_dict_expr } DEDENT
		/// </summary>
		private IEnumerable<KeyValuePair<NodeBase, NodeBase>> parseInitExprDictBlock()
		{
			if (!check(LexemType.Indent))
				yield break;

			var value = parseInitDictExpr();
			if (value != null)
				yield return value.Value;
			else
				error("Initializer expression expected!");

			while (!check(LexemType.Dedent))
			{
				ensure(LexemType.NewLine, "Initializer expressions must be separated by a newline!");
				value = parseInitDictExpr();
				if (value != null)
					yield return value.Value;
				else
					error("Initializer expression expected!");
			}
		}

		/// <summary>
		/// init_dict_expr                              = line_expr "=>" line_expr
		/// </summary>
		private KeyValuePair<NodeBase, NodeBase>? parseInitDictExpr()
		{
			var key = ensure(parseLineExpr, "Dictionary key expression is expected!");
			ensure(LexemType.FatArrow, "Arrow is expected!");
			var value = ensure(parseLineExpr, "Dictionary value expression is expected!");

			return new KeyValuePair<NodeBase, NodeBase>(key, value);
		}

		/// <summary>
		/// new_object_block                            = type invoke_block_args
		/// </summary>
		private NewObjectNode parseNewObjectBlock()
		{
			var type = attempt(parseType);
			if (type == null)
				return null;

			var args = parseInvokeBlockArgs().ToList();
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
		/// invoke_block_expr                           = line_expr { invoke_pass }
		/// </summary>
		private NodeBase parseInvokeBlockExpr()
		{
			var expr = attempt(parseLineExpr);
			if (expr == null)
				return null;

			var pass = attempt(parseInvokePass);
			if (pass == null)
				return null;

			while (true)
			{
				(pass.Expression as GetMemberNode).Expression = expr;
				expr = pass;

				pass = attempt(parseInvokePass);
				if (pass == null)
					return expr;
			}
		}

		/// <summary>
		/// invoke_pass                                 = "|>" identifier ( invoke_block_args | invoke_line_args )
		/// </summary>
		private InvocationNode parseInvokePass()
		{
			if (!check(LexemType.PassRight))
				return null;

			var getter = new GetMemberNode();
			var invoker = new InvocationNode { Expression = getter };

			getter.MemberName = ensure(LexemType.Identifier, "Method name is expected!").Value;

			invoker.Arguments = parseInvokeBlockArgs().ToList();
			if (invoker.Arguments.Count == 0)
				invoker.Arguments = parseInvokeLineArgs().ToList();

			if (invoker.Arguments.Count == 0)
				error("Arguments for method call are expected!");

			return invoker;
		}

		/// <summary>
		/// invoke_block_args                           = INDENT { invoke_block_arg } DEDENT
		/// </summary>
		private IEnumerable<NodeBase> parseInvokeBlockArgs()
		{
			if (!check(LexemType.Indent))
				yield break;

			while (!check(LexemType.Dedent))
				yield return parseInvokeBlockArg();
		}

		/// <summary>
		/// invoke_block_arg                            = "<|" ( ref_arg | expr )
		/// </summary>
		private NodeBase parseInvokeBlockArg()
		{
			if(!check(LexemType.PassLeft))
				error("Left pass is expected before block arguments!");

			return attempt(parseRefArg)
			       ?? ensure(parseExpr, "Expression is expected!");
		}
		
		/// <summary>
		/// invoke_line_args                            = { invoke_line_arg }
		/// </summary>
		private IEnumerable<NodeBase> parseInvokeLineArgs()
		{
			while (true)
			{
				var curr = attempt(parseInvokeLineArg);
				if (curr == null)
					yield break;

				yield return curr;
			}
		}

		/// <summary>
		/// invoke_line_arg                             = ref_arg | get_expr
		/// </summary>
		private NodeBase parseInvokeLineArg()
		{
			return attempt(parseRefArg)
				   ?? ensure(parseGetExpr, "Expression is expected!");
		}

		/// <summary>
		/// ref_arg                                     = "ref" lvalue_expr | "(" "ref" lvalue_expr ")"
		/// </summary>
		private NodeBase parseRefArg()
		{
			var paren = check(LexemType.ParenOpen);

			if (!check(LexemType.Ref))
				return null;

			var node = ensure(parseLvalueExpr, "Lvalue expression is expected!");
			(node as IPointerProvider).PointerRequired = true;

			if (paren)
				ensure(LexemType.ParenClose, "Unmatched paren!");

			return node;
		}

		#endregion

		#region Line expressions

		private NodeBase parseLineExpr()
		{
			throw new NotImplementedException();
		}

		private NodeBase parseLineTypeopExpr()
		{
			
		}

		private NodeBase parseLineOpExpr()
		{
			
		}

		private NodeBase parseTypecheckOpExpr()
		{
			
		}

		private NodeBase parseLineBaseExpr()
		{
			
		}

		private NodeBase parseAtom()
		{
			
		}

		private NodeBase parseParenExpr()
		{
			
		}

		#endregion

		#region Line control structures

		/// <summary>
		/// if_line                                     = if_header line_expr [ "else" line_expr ]
		/// </summary>
		private IfNode parseIfLine()
		{
			var node = attempt(parseIfHeader);
			if (node == null)
				return null;

			node.TrueAction = ensure(parseBlock, "Condition expression is expected!");
			if (check(LexemType.Else))
				node.FalseAction = ensure(parseBlock, "Expression is expected!");

			return node;
		}

		/// <summary>
		/// while_line                                  = while_header line_expr
		/// </summary>
		private WhileNode parseWhileLine()
		{
			var node = attempt(parseWhileHeader);
			if (node == null)
				return null;

			node.Body = ensure(parseBlock, "Loop body expression is expected!");
			return node;
		}

		/// <summary>
		/// for_line                                    = for_header line_expr
		/// </summary>
		private ForeachNode parseForLine()
		{
			var node = attempt(parseForHeader);
			if (node == null)
				return null;

			node.Body = ensure(parseBlock, "Loop body expression is expected!");
			return node;
		}

		/// <summary>
		/// throw_stmt                                  = "throw" [ line_expr ]
		/// </summary>
		private ThrowNode parseThrowStmt()
		{
			if (!check(LexemType.Throw))
				return null;

			var node = new ThrowNode();
			node.Expression = attempt(parseLineExpr);
			return node;
		}

		/// <summary>
		/// yield_stmt                                  = "yield" [ "from" ] line_expr
		/// </summary>
		private NodeBase parseYield()
		{
			// to be merged from Yield branch
			return null;
		}

		#endregion

		#region Line initializers


		#endregion

		#region Literals

		/// <summary>
		/// literal                                     = unit | null | bool | string | int | double | char
		/// </summary>
		private NodeBase parseLiteral()
		{
			return attempt(parseUnit)
			       ?? attempt(parseNull)
			       ?? attempt(parseBool)
			       ?? attempt(parseString)
			       ?? attempt(parseInt)
			       ?? attempt(parseDouble)
			       ?? attempt(parseChar);
		}

		private UnitNode parseUnit()
		{
			return check(LexemType.Unit) ? new UnitNode() : null;
		}

		private NullNode parseNull()
		{
			return check(LexemType.Null) ? new NullNode() : null;
		}

		private BooleanNode parseBool()
		{
			if(check(LexemType.True))
				return new BooleanNode(true);

			if (check(LexemType.False))
				return new BooleanNode();

			return null;
		}

		private StringNode parseString()
		{
			if (!peek(LexemType.String))
				return null;

			return new StringNode(getValue());
		}

		private IntNode parseInt()
		{
			if (!peek(LexemType.Int))
				return null;

			var value = getValue();
			try
			{
				return new IntNode(int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
			}
			catch
			{
				error("Value '{0}' is not a valid integer!", value);
				return null;
			}
		}

		private DoubleNode parseDouble()
		{
			if (!peek(LexemType.Double))
				return null;

			var value = getValue();
			try
			{
				return new DoubleNode(double.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
			}
			catch
			{
				error("Value '{0}' is not a valid double!", value);
				return null;
			}
		}

		private NodeBase parseChar()
		{
			// todo
			return null;
		}

		#endregion
	}
}
