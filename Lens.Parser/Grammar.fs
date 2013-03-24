module Lens.Parser.Grammar

open System
open FParsec
open FParsec.CharParsers
open Lens.Parser.FParsecHelpers
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

let isStartTracked obj = 
    typeof<IStartLocationTrackingEntity>.IsAssignableFrom(obj.GetType())

let isEndTracked obj = 
    typeof<IEndLocationTrackingEntity>.IsAssignableFrom(obj.GetType())

let keywords = Set.ofList ["using"
                           "record"
                           "type"
                           "of"
                           "fun"
                           "let"
                           "var"
                           "while"
                           "if"
                           "else"
                           "try"
                           "catch"
                           "throw"
                           "new"
                           "not"
                           "typeof"
                           "default"
                           "is"
                           "as"
                           "ref"
                           "true"
                           "false"
                           "null"]

let valueToList parser = parser >>= (Seq.singleton >> Seq.toList >> preturn)

let space = pchar ' '
let nextLine = (skipNewline <|> eof) <!> "nextLine"
let keyword k = pstring k .>>? (choice [skipMany1 space
                                        notFollowedBy letter]) <!> sprintf "keyword %s" k
                                                   
let token t = (pstring t .>>? many space) <!> sprintf "token %s" t

let createParser s =
    let parser, parserRef = createParserForwardedToRef()
    let whitespaced = choice [parser .>>? many space
                              many1 space >>. fail "wrong indentation"]
    whitespaced <!> s, parserRef

let createNodeParser name =
    let lexemLocation (position : Position) =
        LexemLocation(Line = int position.Line, Offset = int position.Column)

    let parser, parserRef = createParser name
    let informed (stream : CharStream<ParserState>) : Reply<#NodeBase> =
        let startPosition = stream.Position
        let reply = parser stream
        match reply.Status with
        | Ok -> let endPosition = stream.Position
                let result = reply.Result :> NodeBase
                if isStartTracked result then
                    result.StartLocation <- lexemLocation startPosition
                if isEndTracked result then
                    result.EndLocation <- lexemLocation endPosition
                reply
        | _  -> reply
    informed, parserRef

let annotate parser annotation =
    parser <?> annotation

let createAnnotatedParser name annotation =
    let parser, ref = createParser name
    annotate parser annotation, ref

let createAnnotatedNodeParser name annotation =
    let parser, ref = createNodeParser name
    annotate parser annotation, ref

let stmt, stmtRef                             = createAnnotatedNodeParser "stmt" "statement"
let using, usingRef                           = createAnnotatedNodeParser "using" "using statement"
let ``namespace``, namespaceRef               = createAnnotatedParser "namespace" "namespace declaration"
let recorddef, recorddefRef                   = createAnnotatedNodeParser "recorddef" "record definition"
let recorddef_stmt, recorddef_stmtRef         = createAnnotatedParser "recorddef_stmt" "record definition statement"
let typedef, typedefRef                       = createAnnotatedNodeParser "typedef" "type definition"
let typedef_stmt, typedef_stmtRef             = createAnnotatedParser "typedef_stmt" "type definition statement"
let funcdef, funcdefRef                       = createAnnotatedNodeParser "funcdef" "function definition"
let func_params, func_paramsRef               = createAnnotatedParser "func_params" "function parameters"
let block, blockRef                           = createAnnotatedNodeParser "block" "code block"
let block_line, block_lineRef                 = createAnnotatedNodeParser "block_line" "code block line"
let ``type``, typeRef                         = createAnnotatedParser "type" "type"
let local_stmt, local_stmtRef                 = createAnnotatedNodeParser "local_stmt" "local statement"
let var_decl_expr, var_decl_exprRef           = createAnnotatedNodeParser "var_decl_expr" "variable declaration"
let assign_expr, assign_exprRef               = createAnnotatedNodeParser "assign_expr" "assignment expression"
let lvalue, lvalueRef                         = createAnnotatedParser "lvalue" "lvalue"
let atomar_expr, atomar_exprRef               = createAnnotatedParser "atomar_expr" "atomar_expr"
let accessor_expr, accessor_exprRef           = createAnnotatedParser "accessor_expr" "accessor expression"
let type_params, type_paramsRef               = createAnnotatedParser "type_params" "type parameters"
let expr, exprRef                             = createAnnotatedNodeParser "expr" "expression"
let block_expr, block_exprRef                 = createAnnotatedNodeParser "block_expr" "block expression"
let throw_expr, throw_exprRef                 = createAnnotatedNodeParser "throw_expr" "throw expression"
let if_expr, if_exprRef                       = createAnnotatedNodeParser "if_expr" "if expression"
let while_expr, while_exprRef                 = createAnnotatedNodeParser "while_expr" "while expression"
let try_expr, try_exprRef                     = createAnnotatedNodeParser "try_expr" "try expression"
let catch_expr, catch_exprRef                 = createAnnotatedNodeParser "catch_expr" "catch expression"
let lambda_expr, lambda_exprRef               = createAnnotatedNodeParser "lambda_expr" "lambda expression"
let line_expr, line_exprRef                   = createAnnotatedNodeParser "line_expr" "line expression"
let line_expr_0, line_expr_0Ref               = createAnnotatedNodeParser "line_expr_0" "line expression"
let line_expr_1, line_expr_1Ref               = createAnnotatedNodeParser "line_expr_1" "line expression"
let sign_1, sign_1Ref                         = createAnnotatedParser "sign_1" "operator"
let line_expr_2, line_expr_2Ref               = createAnnotatedNodeParser "line_expr_2" "line expression"
let sign_2, sign_2Ref                         = createAnnotatedParser "sign_2" "operator"
let line_expr_3, line_expr_3Ref               = createAnnotatedNodeParser "line_expr_3" "line expression"
let sign_3, sign_3Ref                         = createAnnotatedParser "sign_3" "operator"
let line_expr_4, line_expr_4Ref               = createAnnotatedNodeParser "line_expr_4" "line expression"
let sign_4, sign_4Ref                         = createAnnotatedParser "sign_4" "operator"
let line_expr_5, line_expr_5Ref               = createAnnotatedNodeParser "line_expr_5" "line expression"
let line_expr_6, line_expr_6Ref               = createAnnotatedNodeParser "line_expr_6" "line expression"
let line_expr_7, line_expr_7Ref               = createAnnotatedNodeParser "line_expr_7" "line expression"
let new_expr, new_exprRef                     = createAnnotatedNodeParser "new_expr" "constructor invocation"
let new_array_expr, new_array_exprRef         = createAnnotatedNodeParser "new_array_expr" "array expression"
let new_tuple_expr, new_tuple_exprRef         = createAnnotatedNodeParser "new_tuple_expr" "tuple expression"
let new_list_expr, new_list_exprRef           = createAnnotatedNodeParser "new_list_expr" "list expression"
let new_dict_expr, new_dict_exprRef           = createAnnotatedNodeParser "new_dict_expr" "dict expression"
let dict_entry_expr, dict_entry_exprRef       = createAnnotatedParser "dict_entry_expr" "dict entry"
let new_obj_expr, new_obj_exprRef             = createAnnotatedNodeParser "new_obj_expr" "new object expression"
let enumeration_expr, enumeration_exprRef     = createAnnotatedParser "enumeration_expr" "enumeration expression"
let invoke_expr, invoke_exprRef               = createAnnotatedNodeParser "invoke_expr" "invocation"
let invoke_list, invoke_listRef               = createAnnotatedParser "invoke_list" "invocation list"
let byref_arg, byref_argRef                   = createAnnotatedParser "byref_arg" "byref argument"
let value_expr, value_exprRef                 = createAnnotatedNodeParser "value_expr" "value"
let rvalue, rvalueRef                         = createAnnotatedParser "rvalue" "rvalue"
let type_operator_expr, type_operator_exprRef = createAnnotatedNodeParser "type_operator_expr" "type operator"
let literal, literalRef                       = createAnnotatedNodeParser "literal" "literal"

let string, stringRef                         = createAnnotatedParser "string" "string literal"
let int, intRef                               = createAnnotatedParser "int" "integer literal"
let double, doubleRef                         = createAnnotatedParser "double" "double literal"
let identifier, identifierRef                 = createAnnotatedParser "identifier" "identifier"

let main               = many newline >>. (many stmt .>>? eof)
stmtRef               := choice [attempt using
                                 attempt recorddef
                                 attempt typedef
                                 attempt funcdef
                                 attempt (local_stmt .>>? nextLine)]
usingRef              := keyword "using" >>? ``namespace`` .>>? nextLine |>> Node.using
namespaceRef          := sepBy1 identifier <| token "." |>> String.concat "."
recorddefRef          := pipe2
                         <| (keyword "record" >>? identifier)
                         <| (Indentation.indentedBlock recorddef_stmt .>>? nextLine)
                         <| Node.record
recorddef_stmtRef     := (identifier .>>.? (token ":" >>? ``type``)) |>> Node.recordEntry
typedefRef            := keyword "type"
                         >>? identifier
                         .>>.? Indentation.indentedBlock typedef_stmt
                         .>>? nextLine
                         |>> Node.typeNode
typedef_stmtRef       := identifier .>>.? opt (keyword "of" >>? ``type``) |>> Node.typeEntry
funcdefRef            := (pipe4
                          <| (keyword "fun" >>? identifier)
                          <| (keyword "of" >>? ``type``)
                          <| (func_params .>>? token "->")
                          <| block
                          <| Node.functionNode) .>>? nextLine
func_paramsRef        := many ((identifier .>>? token ":") .>>.? (opt <| keyword "ref") .>>.? ``type``) |>> Node.functionParameters
blockRef              := ((Indentation.indentedBlock block_line .>>? nextLine)
                          <|> (valueToList local_stmt))
                         |>> Node.codeBlock
block_lineRef         := local_stmt
typeRef               := pipe2
                         <| ``namespace``
                         <| opt ((type_params |>> Node.typeParams) <|> (many (token "[" .>>.? token "]") |>> Node.arrayDefinition))
                         <| Node.typeTag
local_stmtRef         := choice [attempt var_decl_expr
                                 attempt assign_expr
                                 attempt expr]
var_decl_exprRef      := pipe3
                         <| (keyword "let" <|> keyword "var")
                         <| identifier
                         <| (token "=" >>? expr)
                         <| Node.variableDeclaration
assign_exprRef        := pipe2
                         <| lvalue
                         <| (token "=" >>? expr)
                         <| Node.assignment
lvalueRef             := choice [attempt (``type`` .>>? token "::") .>>.? identifier |>> Node.staticSymbol
                                 attempt identifier |>> Node.localSymbol
                                 attempt <| atomar_expr .>>.? accessor_expr |>> Node.expressionSymbol] .>>.? many accessor_expr
atomar_exprRef        := choice [attempt literal
                                 attempt type_operator_expr
                                 attempt <| token "(" >>? expr .>>? token ")"]
accessor_exprRef      := choice [attempt ((token "." >>? identifier) |>> Accessor.Member)
                                 attempt ((token "[" >>? line_expr .>>? token "]") |>> Accessor.Indexer)]
type_paramsRef        := token "<" >>? (sepBy1 ``type`` <| token ",") .>>? token ">"
exprRef               := choice [attempt block_expr
                                 attempt line_expr]
block_exprRef         := choice [attempt if_expr
                                 attempt while_expr
                                 attempt try_expr
                                 attempt throw_expr
                                 attempt lambda_expr]
throw_exprRef         := keyword "throw" >>. opt line_expr |>> Node.throw
if_exprRef            := pipe3
                         <| (keyword "if" >>? (token "(" >>? line_expr .>>? token ")"))
                         <| block
                         <| opt (keyword "else" >>? block)
                         <| Node.ifNode
while_exprRef         := pipe2
                         <| (keyword "while" >>? (token "(" >>? line_expr .>>? token ")"))
                         <| block
                         <| Node.whileNode
try_exprRef           := pipe2
                         <| (keyword "try" >>? block)
                         <| many1 catch_expr
                         <| Node.tryCatchNode
catch_exprRef         := pipe2
                         <| (keyword "catch" >>? opt (token "(" >>? ``type`` .>>.? identifier .>>? token ")"))
                         <| block
                         <| Node.catchNode
lambda_exprRef        := pipe2
                         <| opt (token "(" >>? func_params .>>? token ")")
                         <| (token "->" >>? block)
                         <| Node.lambda
line_exprRef           := pipe2
                          <| attempt line_expr_0
                          <| attempt (opt (Indentation.indentedBlock (token "|>" >>? identifier .>>.? invoke_list)))
                          <| Node.fluentCall
line_expr_0Ref         := pipe2
                         <| line_expr_1
                         <| opt ((keyword "as" <|> keyword "is") .>>.? ``type``)
                         <| Node.castNode
line_expr_1Ref        := pipe2
                         <| line_expr_2
                         <| many (sign_1 .>>.? line_expr_2)
                         <| Node.operatorChain
sign_1Ref             := token "&&" <|> token "||" <|> token "^^"
line_expr_2Ref        := pipe2
                         <| line_expr_3
                         <| many (sign_2 .>>.? line_expr_3)
                         <| Node.operatorChain
sign_2Ref             := choice [attempt <| token "=="
                                 attempt <| token "<>"
                                 attempt <| token "<="
                                 attempt <| token ">="
                                 attempt <| token "<"
                                 attempt <| token ">"]
line_expr_3Ref        := pipe2
                         <| opt (keyword "not" <|> token "-")
                         <| (pipe2
                             <| line_expr_4
                             <| (many (sign_3 .>>.? line_expr_4))
                             <| Node.operatorChain)
                         <| Node.unaryOperator
sign_3Ref             := token "+" <|> token "-"
line_expr_4Ref        := pipe2
                         <| line_expr_5
                         <| (many (sign_4 .>>.? line_expr_5))
                         <| Node.operatorChain
sign_4Ref             := token "*" <|> token "/" <|> token "%"
line_expr_5Ref        := pipe2
                         <| line_expr_6
                         <| (many (token "**" .>>.? line_expr_6))
                         <| Node.operatorChain
line_expr_6Ref        := pipe2
                         <| line_expr_7
                         <| opt (attempt <| token "[" >>? expr .>>? token "]")
                         <| Node.indexNode
line_expr_7Ref        := choice [attempt new_expr
                                 attempt invoke_expr
                                 attempt value_expr]
new_exprRef           := keyword "new" >>? choice [attempt new_array_expr
                                                   attempt new_tuple_expr
                                                   attempt new_list_expr
                                                   attempt new_dict_expr
                                                   attempt new_obj_expr]
new_array_exprRef     := token "[" >>? enumeration_expr .>>? token "]" |>> Node.arrayNode
new_tuple_exprRef     := token "(" >>? enumeration_expr .>>? token ")" |>> Node.tupleNode
new_list_exprRef      := token "<" >>? enumeration_expr .>>? token ">" |>> Node.listNode
new_dict_exprRef      := token "{" >>? (sepBy1 dict_entry_expr <| token ";") .>>? token "}" |>> Node.dictNode
dict_entry_exprRef    := pipe2
                         <| value_expr
                         <| (token "=>" >>? value_expr)
                         <| Node.dictEntry
new_obj_exprRef       := pipe2
                         <| ``type``
                         <| opt (invoke_list)
                         <| Node.objectNode
enumeration_exprRef   := sepBy1 line_expr <| token ";"
invoke_exprRef        := pipe2
                         <| value_expr
                         <| invoke_list
                         <| Node.invocation
invoke_listRef        := (Indentation.indentedBlock (token "<|" >>? choice [attempt expr
                                                                            attempt byref_arg]))
                         <|> ((many1 <| choice [attempt byref_arg
                                                attempt value_expr]) <!> "invoke_list_single_line")
byref_argRef          := choice [attempt (token "(" >>. keyword "ref" .>>. lvalue .>> token ")")
                                 attempt (keyword "ref" .>>. lvalue)]
                         >>. lvalue |>> Node.getterNode
value_exprRef         := choice [attempt rvalue
                                 attempt atomar_expr]
rvalueRef             := pipe2
                         <| attempt lvalue
                         <| (attempt <| opt (type_params))
                         <| Node.genericGetterNode

type_operator_exprRef := pipe2
                         <| (keyword "typeof" <|> keyword "default")
                         <| ``type``
                         <| Node.typeOperator
literalRef            := choice [attempt <| token "()"                         |>> Node.unit
                                 attempt <| keyword "null"                     |>> Node.nullNode
                                 attempt <| keyword "true" <|> keyword "false" |>> Node.boolean
                                 attempt <| string                             |>> Node.string
                                 attempt <| double                             |>> Node.double
                                 attempt <| int                                |>> Node.int]

stringRef             := between <| pchar '"' <| pchar '"' <| regex @"[^""]*"
intRef                := regex @"\d+"
doubleRef             := regex @"\d+\.\d+"
identifierRef         := regex "[a-zA-Z_][0-9a-zA-Z_]*" >>=?
                            fun s -> if Set.contains s keywords then
                                         pzero
                                     else
                                         preturn s
