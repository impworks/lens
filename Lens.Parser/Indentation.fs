module Lens.Parser.Indentation

open FParsec
open Lens.Parser.FParsecHelpers

let private getRealIndent (stream : CharStream<ParserState>) = Reply stream.UserState.RealIndentation
let private getVirtualIndent (stream : CharStream<ParserState>) = Reply stream.UserState.VirtualIndentation
let private setIndent realValue virtualValue (stream : CharStream<ParserState>) =
    stream.UserState <- { stream.UserState with
                              RealIndentation = realValue
                              VirtualIndentation = virtualValue }
    Reply(())

let private skipIndent indent =
    let count = indent * 4
    (skipManyMinMaxSatisfy count count (fun c -> c = ' ')) <!> sprintf "skipIndent %d" indent

let private decreaseIndent indent (stream : CharStream<ParserState>) =
    let state = stream.UserState
    if state.RealIndentation = state.VirtualIndentation
    then let reply = ((many (pstring "    ") |>> List.length) <!> sprintf "decreaseIndent %d" indent) stream
         let count = reply.Result
         if count > indent
         then fail "Incorrect indentation dectected on dedent" stream
         else Reply count
    else Reply state.RealIndentation
    

let private checkIndent (stream : CharStream<ParserState>) =
    let state = stream.UserState
    if state.RealIndentation = state.VirtualIndentation
    then Reply(())
    else fail "Incorrect indentation" stream

let nextLine : Parser<unit, ParserState> =
    parse {
        do! (checkIndent <!> "nextLine > checkIndent")
        let! indent = getRealIndent
        do! skipNewline
        do! skipIndent indent
    } <!> "indented line"

let indent : Parser<unit, ParserState> =
    parse {
        let! indent = getRealIndent
        do! nextLine
        do! skipIndent 1
        do! setIndent (indent + 1) (indent + 1)
    } <!> "indent level increase"

let dedent : Parser<unit, ParserState> =
    parse {
        let! indent = getVirtualIndent
        let! realIndent = decreaseIndent indent
        do! setIndent realIndent (indent - 1)
    } <!> "indent level decrease"
