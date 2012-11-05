module Lens.Parser.Grammar

open System
open FParsec
open FParsec.CharParsers
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

let debug = false

let (<!>) (p : Parser<_,_>) label : Parser<_,_> =
    if debug then
        fun stream ->
            printfn "%A: Entering %s" stream.Position label
            let reply = p stream
            printfn "%A: Leaving %s (%A)" stream.Position label reply.Status
            reply
    else
        p

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
                           "as"
                           "ref"
                           "out"
                           "true"
                           "false"
                           "null"]

let valueToList parser = parser >>= (Seq.singleton >> Seq.toList >> preturn)

let space = pchar ' '
let nextLine = skipNewline <|> eof
let keyword k = pstring k .>>? (choice [skipMany1 space
                                        notFollowedBy letter])
                                                   
let token t = pstring t .>>? many space

let createParser s =
    let parser, parserRef = createParserForwardedToRef()
    let whitespaced = parser .>>? many space
    whitespaced <!> s, parserRef

let createNodeParser s =
    let lexemLocation (position : Position) =
        LexemLocation(Line = int position.Line, Offset = int position.Column)

    let parser, parserRef = createParser s
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

let stmt, stmtRef                             = createNodeParser "stmt"
let using, usingRef                           = createNodeParser "using"
let ``namespace``, namespaceRef               = createParser "namespace"
let recorddef, recorddefRef                   = createNodeParser "recorddef"
let recorddef_stmt, recorddef_stmtRef         = createParser "recorddef_stmt"
let typedef, typedefRef                       = createNodeParser "typedef"
let typedef_stmt, typedef_stmtRef             = createParser "typedef_stmt"
let funcdef, funcdefRef                       = createNodeParser "funcdef"
let func_params, func_paramsRef               = createParser "func_params"
let block, blockRef                           = createNodeParser "block"
let block_line, block_lineRef                 = createNodeParser "block_line"
let ``type``, typeRef                         = createParser "type"
let local_stmt, local_stmtRef                 = createNodeParser "local_stmt"
let var_decl_expr, var_decl_exprRef           = createNodeParser "var_decl_expr"
let assign_expr, assign_exprRef               = createNodeParser "assign_expr"
let lvalue, lvalueRef                         = createParser "lvalue"
let accessor_expr, accessor_exprRef           = createParser "accessor_expr"
let type_params, type_paramsRef               = createParser "type_params"
let expr, exprRef                             = createNodeParser "expr"
let block_expr, block_exprRef                 = createNodeParser "block_expr"
let if_expr, if_exprRef                       = createNodeParser "if_expr"
let while_expr, while_exprRef                 = createNodeParser "while_expr"
let try_expr, try_exprRef                     = createNodeParser "try_expr"
let catch_expr, catch_exprRef                 = createNodeParser "catch_expr"
let lambda_expr, lambda_exprRef               = createNodeParser "lambda_expr"
let line_expr, line_exprRef                   = createNodeParser "line_expr"
let line_expr_1, line_expr_1Ref               = createNodeParser "line_expr_1"
let sign_1, sign_1Ref                         = createParser "sign_1"
let line_expr_2, line_expr_2Ref               = createNodeParser "line_expr_2"
let sign_2, sign_2Ref                         = createParser "sign_2"
let line_expr_3, line_expr_3Ref               = createNodeParser "line_expr_3"
let sign_3, sign_3Ref                         = createParser "sign_3"
let line_expr_4, line_expr_4Ref               = createNodeParser "line_expr_4"
let sign_4, sign_4Ref                         = createParser "sign_4"
let line_expr_5, line_expr_5Ref               = createNodeParser "line_expr_5"
let line_expr_6, line_expr_6Ref               = createNodeParser "line_expr_6"
let line_expr_7, line_expr_7Ref               = createNodeParser "line_expr_7"
let new_expr, new_exprRef                     = createNodeParser "new_expr"
let new_array_expr, new_array_exprRef         = createNodeParser "new_array_expr"
let new_tuple_expr, new_tuple_exprRef         = createNodeParser "new_tuple_expr"
let new_list_expr, new_list_exprRef           = createNodeParser "new_list_expr"
let new_dict_expr, new_dict_exprRef           = createNodeParser "new_dict_expr"
let dict_entry_expr, dict_entry_exprRef       = createParser "dict_entry_expr"
let new_obj_expr, new_obj_exprRef             = createNodeParser "new_obj_expr"
let enumeration_expr, enumeration_exprRef     = createParser "enumeration_expr"
let invoke_expr, invoke_exprRef               = createNodeParser "invoke_expr"
let invoke_list, invoke_listRef               = createParser "invoke_list"
let value_expr, value_exprRef                 = createNodeParser "value_expr"
let type_operator_expr, type_operator_exprRef = createNodeParser "type_operator_expr"
let literal, literalRef                       = createNodeParser "literal"

let string, stringRef                         = createParser "string"
let int, intRef                               = createParser "int"
let double, doubleRef                         = createParser "double"
let identifier, identifierRef                 = createParser "identifier"

let main               = many newline >>. (many stmt .>>? eof)
stmtRef               := using <|> recorddef <|> typedef <|> funcdef <|> (local_stmt .>>? nextLine)
usingRef              := keyword "using" >>? ``namespace`` .>>? nextLine |>> Node.using
namespaceRef          := sepBy1 identifier <| token "." |>> String.concat "."
recorddefRef          := keyword "record" >>? identifier .>>.? IndentationParser.indentedMany1 recorddef_stmt "recorddef_stmt" |>> Node.record
recorddef_stmtRef     := (identifier .>>.? (skipChar ':' >>? ``type``)) |>> Node.recordEntry
typedefRef            := keyword "type" >>? identifier .>>.? IndentationParser.indentedMany1 typedef_stmt "typedef_stmt" |>> Node.typeNode
typedef_stmtRef       := token "|" >>? identifier .>>.? opt (keyword "of" >>? ``type``) |>> Node.typeEntry
funcdefRef            := pipe3
                         <| (keyword "fun" >>? identifier)
                         <| (func_params .>>? token "->")
                         <| block
                         <| Node.functionNode
func_paramsRef        := many ((identifier .>>? token ":") .>>.? (opt (keyword "ref" <|> keyword "out")) .>>.? ``type``) |>> Node.functionParameters
blockRef              := ((IndentationParser.indentedMany1 block_line "block_line")
                          <|> (valueToList local_stmt))
                         |>> Node.codeBlock
block_lineRef         := local_stmt
typeRef               := pipe2
                         <| ``namespace``
                         <| opt (type_params <|> (many (token "[" .>>.? token "]") |>> Node.arrayDefinition))
                         <| Node.typeTag
local_stmtRef         := choice [attempt var_decl_expr
                                 attempt assign_expr
                                 expr]
var_decl_exprRef      := pipe3
                         <| (keyword "let" <|> keyword "var")
                         <| identifier
                         <| (token "=" >>? expr)
                         <| Node.variableDeclaration
assign_exprRef        := pipe2
                         <| lvalue
                         <| (token "=" >>? expr)
                         <| Node.assignment
lvalueRef             := choice [(``type`` .>>? token "::") .>>.? identifier |>> Node.staticSymbol
                                 identifier |>> Node.localSymbol] .>>.? many accessor_expr
accessor_exprRef      := ((token "." >>? identifier) |>> Accessor.Member)
                         <|> ((token "[" >>? line_expr .>>? token "]") |>> Accessor.Indexer)
type_paramsRef        := token "<" >>? (sepBy1 ``type`` <| token ",") .>>? token ">" |>> Node.typeParams
exprRef               := block_expr <|> line_expr
block_exprRef         := if_expr <|> while_expr <|> try_expr <|> lambda_expr
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
line_exprRef          := pipe2
                         <| line_expr_1
                         <| opt (keyword "as" >>? ``type``)
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
sign_2Ref             := token "==" <|> token "<>" <|> token "<" <|> token ">" <|> token "<=" <|> token ">="
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
                         <| opt (token "[" >>? expr .>>? token "]")
                         <| Node.indexNode
line_expr_7Ref        := choice [attempt new_expr
                                 attempt invoke_expr
                                 value_expr]
new_exprRef           := keyword "new" >>? choice [new_array_expr
                                                   new_tuple_expr
                                                   new_list_expr
                                                   new_dict_expr
                                                   new_obj_expr]
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
invoke_listRef        := (many1 (newline >>? (token "<|" >>? value_expr)) .>>? nextLine) <|> (many1 value_expr)
value_exprRef         := choice [literal
                                 type_operator_expr
                                 token "(" >>? expr .>>? token ")"
                                 lvalue |>> Node.getterNode]
type_operator_exprRef := pipe2
                         <| (keyword "typeof" <|> keyword "default")
                         <| (token "(" >>? ``type`` .>>? token ")")
                         <| Node.typeOperator
literalRef            := choice [token "()"                         |>> Node.unit
                                 keyword "null"                     |>> Node.nullNode
                                 keyword "true" <|> keyword "false" |>> Node.boolean
                                 string                             |>> Node.string
                                 double                             |>> Node.double
                                 int                                |>> Node.int]

stringRef             := between <| pchar '"' <| pchar '"' <| regex @"[^""]*"
intRef                := regex @"\d+"
doubleRef             := regex @"\d+.\d+"
identifierRef         := regex "[a-zA-Z_][0-9a-zA-Z_]*" >>=?
                            fun s -> if Set.contains s keywords then
                                         pzero
                                     else
                                         preturn s
