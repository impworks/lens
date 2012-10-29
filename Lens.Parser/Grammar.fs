module Lens.Parser.Grammar

open FParsec
open FParsec.CharParsers
open Lens.SyntaxTree.SyntaxTree

let space = pchar ' '
let nextLine = skipNewline <|> eof
let keyword k = pstring k .>> many1 space
let token t = pstring t .>> many space

let createParser() =
    let parser, parserRef = createParserForwardedToRef()
    let whitespaced = parser .>> many space
    whitespaced, parserRef

let stmt, stmtRef                             = createParser()
let using, usingRef                           = createParser()
let ``namespace``, namespaceRef               = createParser()
let recorddef, recorddefRef                   = createParser()
let recorddef_stmt, recorddef_stmtRef         = createParser()
let typedef, typedefRef                       = createParser()
let typedef_stmt, typedef_stmtRef             = createParser()
let funcdef, funcdefRef                       = createParser()
let func_params, func_paramsRef               = createParser()
let block, blockRef                           = createParser()
let block_line, block_lineRef                 = createParser()
let ``type``, typeRef                         = createParser()
let local_stmt, local_stmtRef                 = createParser()
let assign_expr, assign_exprRef               = createParser()
let rvalue, rvalueRef                         = createParser()
let accessor_expr, accessor_exprRef           = createParser()
let type_params, type_paramsRef               = createParser()
let expr, exprRef                             = createParser()
let block_expr, block_exprRef                 = createParser()
let if_expr, if_exprRef                       = createParser()
let while_expr, while_exprRef                 = createParser()
let try_expr, try_exprRef                     = createParser()
let catch_expr, catch_exprRef                 = createParser()
let lambda_expr, lambda_exprRef               = createParser()
let line_expr, line_exprRef                   = createParser()
let line_expr_1, line_expr_1Ref               = createParser()
let sign_1, sign_1Ref                         = createParser()
let line_expr_2, line_expr_2Ref               = createParser()
let sign_2, sign_2Ref                         = createParser()
let line_expr_3, line_expr_3Ref               = createParser()
let sign_3, sign_3Ref                         = createParser()
let line_expr_4, line_expr_4Ref               = createParser()
let sign_4, sign_4Ref                         = createParser()
let line_expr_5, line_expr_5Ref               = createParser()
let line_expr_6, line_expr_6Ref               = createParser()
let line_expr_7, line_expr_7Ref               = createParser()
let new_expr, new_exprRef                     = createParser()
let new_array_expr, new_array_exprRef         = createParser()
let new_tuple_expr, new_tuple_exprRef         = createParser()
let new_obj_expr, new_obj_exprRef             = createParser()
let enumeration_expr, enumeration_exprRef     = createParser()
let invoke_expr, invoke_exprRef               = createParser()
let invoke_list, invoke_listRef               = createParser()
let value_expr, value_exprRef                 = createParser()
let type_operator_expr, type_operator_exprRef = createParser()
let literal, literalRef                       = createParser()

let string, stringRef                         = createParser()
let int, intRef                               = createParser()
let identifier, identifierRef                 = createParser()

let main               = many stmt .>> eof
stmtRef               := using <|> recorddef <|> typedef <|> funcdef <|> (local_stmt .>> nextLine)
usingRef              := keyword "using" >>. ``namespace`` .>> nextLine |>> Node.using
namespaceRef          := sepBy1 identifier <| token "::" |>> String.concat "."
recorddefRef          := keyword "record" >>. identifier .>>. IndentationParser.indentedMany1 recorddef_stmt "recorddef_stmt" |>> Node.record
recorddef_stmtRef     := (identifier .>>. (skipChar ':' >>. ``type``)) |>> Node.recordEntry
typedefRef            := keyword "type" >>. identifier .>>. IndentationParser.indentedMany1 typedef_stmt "typedef_stmt" |>> Node.typeNode
typedef_stmtRef       := token "|" >>. identifier .>>. opt (token "of" >>. ``type``) |>> Node.typeEntry
funcdefRef            := pzero<NodeBase, ParserState> (* TODO: "fun" identifier func_params "->" block *)
func_paramsRef        := pzero<NodeBase, ParserState> (* TODO: { identifier ":" [ ( "ref" | "out" ) ] type } *)
blockRef              := pzero<NodeBase, ParserState> (* TODO: NL block_line { block_line } | line_expr *)
block_lineRef         := pzero<NodeBase, ParserState> (* TODO: INDENT local_stmt NL *)
typeRef               := pipe3
                         <| opt (attempt (``namespace`` .>> token "."))
                         <| identifier
                         <| opt (* TODO: { "[]" } | *) type_params
                         <| Node.typeTag
local_stmtRef         := (* TODO: assign_expr | *) expr
assign_exprRef        := pzero<NodeBase, ParserState> (* ( [ "let" | "var" ] identifier | rvalue ) "=" expr *)
rvalueRef             := pzero<NodeBase, ParserState> (* ( type | "(" line_expr ")" ) accessor_expr { accessor_expr } *)
accessor_exprRef      := pzero<NodeBase, ParserState> (* "." identifier | "[" line_expr "]" *)
type_paramsRef        := pzero (* TODO: "<" type { "," type } ">" *)
exprRef               := (* TODO: block_expr | *) line_expr
block_exprRef         := pzero<NodeBase, ParserState> (* if_expr | while_expr | try_expr | lambda_expr *)
if_exprRef            := pzero<NodeBase, ParserState> (* "if" "(" line_expr ")" block [ "else" block ] *)
while_exprRef         := pzero<NodeBase, ParserState> (* "while" "(" line_expr ")" block *)
try_exprRef           := pzero<NodeBase, ParserState> (* "try" block catch_expr { catch_expr } *)
catch_exprRef         := pzero<NodeBase, ParserState> (* "catch" [ "(" type identifier ")" ] block *)
lambda_exprRef        := pzero<NodeBase, ParserState> (* [ "(" func_params ")" ] "->" block *)
line_exprRef          := line_expr_1 (* TODO: [ "as" type ] *)
line_expr_1Ref        := line_expr_2 (* TODO: { sign_1 line_expr_2 } *)
sign_1Ref             := pzero<NodeBase, ParserState> (* TODO: "&&" | "||" | "^^" *)
line_expr_2Ref        := line_expr_3 (* TODO: { sign_2 line_expr_3 } *)
sign_2Ref             := pzero<NodeBase, ParserState> (* TODO: "==" | "<>" | "<" | ">" | "<=" | ">=" *)
line_expr_3Ref        := (* TODO: [ "not" | "-" ] *) (line_expr_4 .>>. (many (sign_3 .>>. line_expr_4))) |>> Node.operatorChain
sign_3Ref             := pstring "+" <|> pstring "-"
line_expr_4Ref        := line_expr_5 (* TODO: { sign_4 line_expr_5 } *)
sign_4Ref             := pzero<NodeBase, ParserState> (* TODO: "*" | "/" | "%" *)
line_expr_5Ref        := line_expr_6 (* TODO: { "**" line_expr_6 } *)
line_expr_6Ref        := line_expr_7 (* TODO: { "[" expr "]" } *)
line_expr_7Ref        := (* TODO: new_expr | *) invoke_expr
new_exprRef           := pzero<NodeBase, ParserState> (* TODO: "new" ( new_array_expr | new_tuple_expr | new_obj_expr ) *)
new_array_exprRef     := pzero<NodeBase, ParserState> (* TODO: "[" enumeration_expr "]" *)
new_tuple_exprRef     := pzero<NodeBase, ParserState> (* TODO: "(" enumeration_expr ")" *)
new_obj_exprRef       := pzero<NodeBase, ParserState> (* TODO: type [ invoke_list ] *)
enumeration_exprRef   := pzero<NodeBase, ParserState> (* TODO: line_expr { ";" line_expr } *)
invoke_exprRef        := value_expr (* TODO: value_expr [ invoke_list ] *)
invoke_listRef        := pzero<NodeBase, ParserState> (* TODO: { value_expr } | ( { NL "<|" value_expr } NL ) *)
value_exprRef         := (* TODO: type { accessor_expr } | *) literal (* TODO: | type_operator_expr | "(" expr ")" *)
type_operator_exprRef := pzero<NodeBase, ParserState> (* TODO: ( "typeof" | "default" ) "(" type ")" *)
literalRef            := (* TODO: "()" | "null" | "true" | "false" | string | *) int |>> Node.int

stringRef             := pzero<NodeBase, ParserState> (* TODO: ... *)
intRef                := regex "\d+"
identifierRef         := regex "[a-zA-Z_]+"
