module Lens.Parser.Indentation

open FParsec
open Lens.Parser.FParsecHelpers
open Lens.SyntaxTree.Translations

let private getRealIndent (stream : CharStream<ParserState>) = Reply stream.UserState.RealIndentation
let private getVirtualIndent (stream : CharStream<ParserState>) = Reply stream.UserState.VirtualIndentation
let private setIndent realValue virtualValue =
    updateUserState (fun state -> { state with RealIndentation = realValue
                                               VirtualIndentation = virtualValue })

let private ensureIndent indent (stream : CharStream<ParserState>) =
    let realCount = int stream.Column - 1
    let count = indent * 4
    let toSkip = count - realCount
    if toSkip < 0
    then fail ParserMessages.IncorrectIndentation stream
    else skipManyMinMaxSatisfy toSkip toSkip (fun c -> c = ' ') stream

let private decreaseIndent indent (stream : CharStream<ParserState>) =
    let state = stream.UserState
    if state.RealIndentation = state.VirtualIndentation
    then let reply = ((many (pstring "    ") |>> List.length) <!> sprintf "decreaseIndent %d" indent) stream
         let count = reply.Result
         if count > indent
         then fail ParserMessages.IncorrectIndentation stream
         else Reply count
    else Reply state.RealIndentation

// Skips one or more lines in not FreshNewline state. Skips zero or more lines in FreshNewline state.
let private skipNewlines (stream : CharStream<ParserState>) =
    let state = stream.UserState
    let column = int stream.Column
    let fresh = state.RealIndentation * 4 = (column - 1)
    
    if debug
    then printfn "Inside skipNewlines with fresh = %A" fresh

    let skipper = if fresh
                  then many skipNewline
                  else many1 skipNewline
    (skipper >>. preturn(())) stream

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
    else fail ParserMessages.IncorrectIndentation stream

/// Line separator for indented lines. Preserves indentation.
let nextLine : Parser<unit, ParserState> =
    parse {
        do! (checkIndent <!> "nextLine > checkIndent")
        let! indent = getRealIndent
        do! skipNewlines
        do! (ensureIndent indent) <!> sprintf "ensureIndent %d" indent
    } <!> "indented line"

let indent : Parser<unit, ParserState> =
    parse {
        let! indent = getRealIndent
        do! nextLine
        do! ensureIndent (indent + 1)
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
    (indent >>. sepBy1 blockLineParser (attempt nextLine) .>> dedent) <!> "indented block"
