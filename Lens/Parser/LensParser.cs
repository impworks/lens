using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.Compiler;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;

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
	}
}
