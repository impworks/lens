module Lens.Parser.Grammar

open System
open FParsec
open FParsec.CharParsers
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

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

let isTracked obj =
    let notTrackedNodes = [typeof<BinaryOperatorNodeBase>]
    let objType = obj.GetType()
    not <| List.exists (fun (t : Type) -> t.IsAssignableFrom(objType)) notTrackedNodes

let valueToList parser = parser >>= (Seq.singleton >> Seq.toList >> preturn)

let space = pchar ' '
let nextLine = skipNewline <|> eof
let keyword k = pstring k .>>? many1 space
let token t = pstring t .>>? many space

let createParser() =
    let parser, parserRef = createParserForwardedToRef()
    let whitespaced = parser .>>? many space
    whitespaced, parserRef

let createNodeParser() =
    let lexemLocation (position : Position) =
        LexemLocation(Line = int position.Line, Offset = int position.Column)

    let parser, parserRef = createParser()
    let informed (stream : CharStream<ParserState>) : Reply<#NodeBase> =
        let startPosition = stream.Position
        let reply = parser stream
        match reply.Status with
        | Ok -> let endPosition = stream.Position
                let result = reply.Result :> NodeBase
                if isTracked result then
                    result.StartLocation <- lexemLocation startPosition
                    result.EndLocation <- lexemLocation endPosition
                reply
        | _  -> reply
    informed, parserRef

let stmt, stmtRef                             = createNodeParser()
let using, usingRef                           = createNodeParser()
let ``namespace``, namespaceRef               = createParser()
let recorddef, recorddefRef                   = createNodeParser()
let recorddef_stmt, recorddef_stmtRef         = createParser()
let typedef, typedefRef                       = createNodeParser()
let typedef_stmt, typedef_stmtRef             = createParser()
let funcdef, funcdefRef                       = createNodeParser()
let func_params, func_paramsRef               = createParser()
let block, blockRef                           = createNodeParser()
let block_line, block_lineRef                 = createNodeParser()
let ``type``, typeRef                         = createParser()
let local_stmt, local_stmtRef                 = createNodeParser()
let var_decl_expr, var_decl_exprRef           = createNodeParser()
let assign_expr, assign_exprRef               = createNodeParser()
let accessor_expr, accessor_exprRef           = createParser()
let type_params, type_paramsRef               = createParser()
let expr, exprRef                             = createNodeParser()
let block_expr, block_exprRef                 = createNodeParser()
let if_expr, if_exprRef                       = createNodeParser()
let while_expr, while_exprRef                 = createNodeParser()
let try_expr, try_exprRef                     = createNodeParser()
let catch_expr, catch_exprRef                 = createNodeParser()
let lambda_expr, lambda_exprRef               = createNodeParser()
let line_expr, line_exprRef                   = createNodeParser()
let line_expr_1, line_expr_1Ref               = createNodeParser()
let sign_1, sign_1Ref                         = createParser()
let line_expr_2, line_expr_2Ref               = createNodeParser()
let sign_2, sign_2Ref                         = createParser()
let line_expr_3, line_expr_3Ref               = createNodeParser()
let sign_3, sign_3Ref                         = createParser()
let line_expr_4, line_expr_4Ref               = createNodeParser()
let sign_4, sign_4Ref                         = createParser()
let line_expr_5, line_expr_5Ref               = createNodeParser()
let line_expr_6, line_expr_6Ref               = createNodeParser()
let line_expr_7, line_expr_7Ref               = createNodeParser()
let new_expr, new_exprRef                     = createNodeParser()
let new_array_expr, new_array_exprRef         = createNodeParser()
let new_tuple_expr, new_tuple_exprRef         = createNodeParser()
let new_obj_expr, new_obj_exprRef             = createNodeParser()
let enumeration_expr, enumeration_exprRef     = createParser()
let invoke_expr, invoke_exprRef               = createNodeParser()
let invoke_list, invoke_listRef               = createParser()
let value_expr, value_exprRef                 = createNodeParser()
let type_operator_expr, type_operator_exprRef = createNodeParser()
let literal, literalRef                       = createNodeParser()

let string, stringRef                         = createParser()
let int, intRef                               = createParser()
let identifier, identifierRef                 = createParser()

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
                          <|> (valueToList line_expr))
                         |>> Node.codeBlock
block_lineRef         := local_stmt
typeRef               := pipe3
                         <| opt (``namespace`` .>>? token ".")
                         <| identifier
                         <| opt (type_params <|> (many (token "[" .>>.? token "]") |>> Node.arrayDefinition))
                         <| Node.typeTag
local_stmtRef         := var_decl_expr <|> assign_expr <|> expr
var_decl_exprRef      := pipe3
                         <| (keyword "let" <|> keyword "var")
                         <| identifier
                         <| (token "=" >>? expr)
                         <| Node.variableDeclaration
assign_exprRef        := pipe4
                         <| ``type``
                         <| opt (token "::" >>? identifier)
                         <| many accessor_expr
                         <| (token "=" >>? expr)
                         <| Node.assignment
accessor_exprRef      := ((token "." >>? identifier) |>> Node.Accessor.Member)
                         <|> ((token "[" >>? line_expr .>>? token "]") |>> Node.Accessor.Indexer)
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
                         <| opt (keyword "as" .>>? ``type``)
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
sign_3Ref             := pstring "+" <|> pstring "-"
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
line_expr_7Ref        := new_expr <|> invoke_expr
new_exprRef           := keyword "new" >>? (new_array_expr <|> new_tuple_expr <|> new_obj_expr)
new_array_exprRef     := token "[" >>? enumeration_expr .>>? token "]" |>> Node.arrayNode
new_tuple_exprRef     := token "(" >>? enumeration_expr .>>? token ")" |>> Node.tupleNode
new_obj_exprRef       := pipe2
                         <| ``type``
                         <| opt (invoke_list)
                         <| Node.objectNode
enumeration_exprRef   := sepBy1 line_expr <| token ";"
invoke_exprRef        := pipe2
                         <| value_expr
                         <| opt invoke_list
                         <| Node.invocation
invoke_listRef        := (many (newline >>? (token "<|" >>? value_expr)) .>>? nextLine) <|> (many value_expr)
value_exprRef         := choice [pipe2 ``type`` <| many accessor_expr <| Node.staticAccessor
                                 literal
                                 type_operator_expr
                                 token "(" >>? expr .>>? token ")"]
type_operator_exprRef := pipe2
                         <| (keyword "typeof" <|> keyword "default")
                         <| (token "(" .>>? ``type`` .>>? token ")")
                         <| Node.typeOperator
literalRef            := choice [token "()"                         |>> Node.unit
                                 keyword "null"                     |>> Node.nullNode
                                 keyword "true" <|> keyword "false" |>> Node.boolean
                                 string                             |>> Node.string
                                 int                                |>> Node.int]

stringRef             := between <| pchar '"' <| pchar '"' <| (manyChars anyChar)
intRef                := regex "\d+"
identifierRef         := regex "[a-zA-Z_][0-9a-zA-Z_]*" >>=?
                            fun s -> if Set.contains s keywords then
                                         pzero
                                     else
                                         preturn s
