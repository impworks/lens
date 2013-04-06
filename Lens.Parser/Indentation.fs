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

let private skipNewlines = (many1 skipNewline) >>. preturn(())

/// Go to new line if current indentation level is "real" (i.e. real indentation is equals to virtual indentation).
/// This magic is used for dedent only.
let private goToNewLine (stream : CharStream<ParserState>) =
    let state = stream.UserState
    if state.RealIndentation = state.VirtualIndentation
    then skipNewlines stream
    else Reply(())

let private checkIndent (stream : CharStream<ParserState>) =
    let state = stream.UserState
    if state.RealIndentation = state.VirtualIndentation
    then Reply(())
    else fail "Incorrect indentation" stream

/// Line separator for indented lines. Preserves indentation.
let nextLine : Parser<unit, ParserState> =
    parse {
        do! (checkIndent <!> "nextLine > checkIndent")
        let! indent = getRealIndent
        do! skipNewlines
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
    eof <|>
    parse {
        do! goToNewLine
        let! indent = getVirtualIndent
        let! realIndent = decreaseIndent indent
        do! setIndent realIndent (indent - 1)
    } <!> "indent level decrease"

/// Parse sequence of blockLineParser, all indented and separated by newline character.
let indentedBlockOf blockLineParser =
    indent >>. sepBy1 blockLineParser (attempt nextLine) .>> dedent
